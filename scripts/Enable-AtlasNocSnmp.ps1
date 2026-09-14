[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Community,

    [Parameter(Mandatory)]
    [ValidatePattern('^(25[0-5]|2[0-4]\d|1?\d?\d)(\.(25[0-5]|2[0-4]\d|1?\d?\d)){3}$')]
    [string]$WorkerIp
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Ejecuta este script en PowerShell como Administrador.'
}

$validCommunities = 'HKLM:\SYSTEM\CurrentControlSet\Services\SNMP\Parameters\ValidCommunities'
$permittedManagers = 'HKLM:\SYSTEM\CurrentControlSet\Services\SNMP\Parameters\PermittedManagers'
$firewallName = 'AtlasNOC SNMP UDP 161'

if ($PSCmdlet.ShouldProcess('Windows SNMP agent', 'Install and configure read-only access')) {
    Add-WindowsCapability -Online -Name 'SNMP.Client~~~~0.0.1.0' | Out-Null
    Add-WindowsCapability -Online -Name 'WMI-SNMP-Provider.Client~~~~0.0.1.0' | Out-Null

    New-Item -Path $validCommunities -Force | Out-Null
    New-ItemProperty -Path $validCommunities -Name $Community -PropertyType DWord -Value 4 -Force | Out-Null

    New-Item -Path $permittedManagers -Force | Out-Null
    Remove-ItemProperty -Path $permittedManagers -Name '*' -ErrorAction SilentlyContinue
    New-ItemProperty -Path $permittedManagers -Name '1' -PropertyType String -Value $WorkerIp -Force | Out-Null

    Get-NetFirewallRule -DisplayName $firewallName -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName $firewallName -Direction Inbound -Protocol UDP -LocalPort 161 `
        -RemoteAddress $WorkerIp -Action Allow -Profile Domain,Private | Out-Null

    Set-Service -Name 'SNMP' -StartupType Automatic
    Restart-Service -Name 'SNMP' -Force
}

$service = Get-Service -Name 'SNMP'
Write-Host "SNMP: $($service.Status)"
Write-Host "Community configurada como solo lectura."
Write-Host "Manager permitido: $WorkerIp"
Write-Host "UDP/161 permitido únicamente desde: $WorkerIp"
