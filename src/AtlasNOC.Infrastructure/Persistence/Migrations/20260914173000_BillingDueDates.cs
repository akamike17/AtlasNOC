using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AtlasNOC.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AtlasNOCDbContext))]
[Migration("20260914173000_BillingDueDates")]
public partial class BillingDueDates : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<DateTime>("DueAtUtc", "BillingEntries", type: "datetime(6)", nullable: true);
        m.AddColumn<string>("Period", "BillingEntries", type: "varchar(64)", maxLength: 64, nullable: true);
        m.CreateIndex("IX_BillingEntries_DueAtUtc", "BillingEntries", "DueAtUtc");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropIndex("IX_BillingEntries_DueAtUtc", "BillingEntries");
        m.DropColumn("DueAtUtc", "BillingEntries");
        m.DropColumn("Period", "BillingEntries");
    }
}
