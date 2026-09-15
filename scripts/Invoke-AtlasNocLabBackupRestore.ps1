[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Server,
    [int] $Port = 3306,
    [Parameter(Mandatory)] [string] $User,
    [Parameter(Mandatory)] [string] $Password,
    [Parameter(Mandatory)] [string] $SourceDatabase,
    [Parameter(Mandatory)] [string] $RestoreDatabase,
    [string] $OutputDirectory = (Join-Path (Get-Location) '.lab-backups')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
foreach ($db in @($SourceDatabase, $RestoreDatabase)) {
    if ($db -notmatch '(?i)(test|lab|e2e|integration|runtime)') {
        throw "Refusing backup/restore for non-LAB database '$db'."
    }
}
$mysql = (Get-Command mysql.exe -ErrorAction SilentlyContinue)?.Source
$dump = (Get-Command mysqldump.exe -ErrorAction SilentlyContinue)?.Source
if (-not $mysql) { $mysql = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe' }
if (-not $dump) { $dump = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe' }
if (-not (Test-Path -LiteralPath $mysql) -or -not (Test-Path -LiteralPath $dump)) {
    throw 'mysql.exe/mysqldump.exe not found.'
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$archive = Join-Path $OutputDirectory "$SourceDatabase-$stamp.sql"
$env:MYSQL_PWD = $Password
try {
    & $dump --protocol=tcp --host=$Server --port=$Port --user=$User --single-transaction --routines --triggers --events --databases $SourceDatabase --result-file=$archive
    if ($LASTEXITCODE -ne 0) { throw "mysqldump failed with exit code $LASTEXITCODE" }
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    & $mysql --protocol=tcp --host=$Server --port=$Port --user=$User --execute="DROP DATABASE IF EXISTS ``$RestoreDatabase``; CREATE DATABASE ``$RestoreDatabase``;"
    if ($LASTEXITCODE -ne 0) { throw "restore database preparation failed with exit code $LASTEXITCODE" }
    $sqlArchive = $archive.Replace('\', '/')
    # A dump made with --databases contains CREATE/USE for the source. Strip
    # those database selectors so SOURCE cannot silently restore into source.
    $sql = Get-Content -LiteralPath $archive -Raw
    $escapedSource = [regex]::Escape($SourceDatabase)
    $sql = [regex]::Replace($sql, "(?im)^CREATE DATABASE.*$escapedSource.*\r?\n", '')
    $sourceUse = "USE ``$SourceDatabase``;"
    $targetUse = "USE ``$RestoreDatabase``;"
    $sql = $sql.Replace($sourceUse, $targetUse)
    Set-Content -LiteralPath $archive -Value $sql -Encoding utf8
    & $mysql --protocol=tcp --host=$Server --port=$Port --user=$User --database=$RestoreDatabase --execute="SOURCE $sqlArchive;"
    if ($LASTEXITCODE -ne 0) { throw "mysql restore failed with exit code $LASTEXITCODE" }
    $tables = (& $mysql --protocol=tcp --host=$Server --port=$Port --user=$User --database=$RestoreDatabase --batch --skip-column-names --execute='SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE();').Trim()
    [pscustomobject]@{ Archive = $archive; Sha256 = $hash; RestoredDatabase = $RestoreDatabase; TableCount = [int]$tables }
}
finally { Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue }
