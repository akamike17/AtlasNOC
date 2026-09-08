using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveryRunLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "DiscoveryRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAtUtc",
                table: "DiscoveryRuns",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBy",
                table: "DiscoveryRuns",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAtUtc",
                table: "DiscoveryRuns",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryRuns_Status_LeaseExpiresAtUtc_StartedAtUtc",
                table: "DiscoveryRuns",
                columns: new[] { "Status", "LeaseExpiresAtUtc", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiscoveryRuns_Status_LeaseExpiresAtUtc_StartedAtUtc",
                table: "DiscoveryRuns");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "DiscoveryRuns");

            migrationBuilder.DropColumn(
                name: "ClaimedAtUtc",
                table: "DiscoveryRuns");

            migrationBuilder.DropColumn(
                name: "ClaimedBy",
                table: "DiscoveryRuns");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                table: "DiscoveryRuns");
        }
    }
}
