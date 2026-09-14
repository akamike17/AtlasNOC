using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations;

/// <summary>Asocia cada credencial de control a un dispositivo y driver concretos.</summary>
[DbContext(typeof(AtlasNOCDbContext))]
[Migration("20260914170000_ScopeDeviceCredentials")]
public partial class ScopeDeviceCredentials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("DeviceId", "DeviceCredentials", type: "char(36)", nullable: true,
            collation: "ascii_general_ci");
        migrationBuilder.AddColumn<Guid>("SiteId", "DeviceCredentials", type: "char(36)", nullable: true,
            collation: "ascii_general_ci");
        migrationBuilder.AddColumn<string>("DriverKey", "DeviceCredentials", type: "varchar(64)", maxLength: 64,
            nullable: true);
        migrationBuilder.AddColumn<bool>("IsPreferred", "DeviceCredentials", type: "tinyint(1)", nullable: false,
            defaultValue: false);
        migrationBuilder.CreateIndex("IX_DeviceCredentials_DeviceId_DriverKey_IsPreferred", "DeviceCredentials",
            new[] { "DeviceId", "DriverKey", "IsPreferred" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_DeviceCredentials_DeviceId_DriverKey_IsPreferred", "DeviceCredentials");
        migrationBuilder.DropColumn("DeviceId", "DeviceCredentials");
        migrationBuilder.DropColumn("SiteId", "DeviceCredentials");
        migrationBuilder.DropColumn("DriverKey", "DeviceCredentials");
        migrationBuilder.DropColumn("IsPreferred", "DeviceCredentials");
    }
}
