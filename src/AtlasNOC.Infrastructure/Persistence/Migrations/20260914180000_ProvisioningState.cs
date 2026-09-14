using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AtlasNOC.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AtlasNOCDbContext))]
[Migration("20260914180000_ProvisioningState")]
public partial class ProvisioningState : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<int>("Provisioning", "CustomerServices", type: "int", nullable: false, defaultValue: 0);
        m.AddColumn<string>("ProvisioningEvidence", "CustomerServices", type: "varchar(1000)", nullable: true);
        m.AddColumn<int>("Provisioning", "CpeAuthorizationCases", type: "int", nullable: false, defaultValue: 0);
        m.AddColumn<string>("EnforcementEvidence", "CpeAuthorizationCases", type: "varchar(1000)", nullable: true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropColumn("Provisioning", "CustomerServices"); m.DropColumn("ProvisioningEvidence", "CustomerServices");
        m.DropColumn("Provisioning", "CpeAuthorizationCases"); m.DropColumn("EnforcementEvidence", "CpeAuthorizationCases");
    }
}
