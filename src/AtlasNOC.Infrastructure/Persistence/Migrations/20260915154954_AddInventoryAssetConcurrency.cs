using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAssetConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RowVersion",
                table: "InventoryAssets",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryAssets");
        }
    }
}
