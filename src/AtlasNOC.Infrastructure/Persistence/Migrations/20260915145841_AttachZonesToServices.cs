using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttachZonesToServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "CustomerServices",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerServices_ZoneId",
                table: "CustomerServices",
                column: "ZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerServices_NetworkZones_ZoneId",
                table: "CustomerServices",
                column: "ZoneId",
                principalTable: "NetworkZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerServices_NetworkZones_ZoneId",
                table: "CustomerServices");

            migrationBuilder.DropIndex(
                name: "IX_CustomerServices_ZoneId",
                table: "CustomerServices");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "CustomerServices");
        }
    }
}
