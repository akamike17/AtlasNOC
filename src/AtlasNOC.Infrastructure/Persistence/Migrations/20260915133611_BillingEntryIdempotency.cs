using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations;

public partial class BillingEntryIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("IdempotencyKey", "BillingEntries", type: "varchar(128)", maxLength: 128, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_BillingEntries_AccountId_Type_IdempotencyKey", "BillingEntries", new[] { "AccountId", "Type", "IdempotencyKey" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_BillingEntries_AccountId_Type_IdempotencyKey", "BillingEntries");
        migrationBuilder.DropColumn("IdempotencyKey", "BillingEntries");
    }
}
