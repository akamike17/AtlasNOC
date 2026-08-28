using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Domain.Migrations
{
    /// <inheritdoc />
    public partial class PersistDiscoveryRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscoveryRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SubnetCidr = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TargetsScanned = table.Column<int>(type: "int", nullable: false),
                    TargetsReachable = table.Column<int>(type: "int", nullable: false),
                    DevicesJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryRuns", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryRuns_StartedAt",
                table: "DiscoveryRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryRuns_Status",
                table: "DiscoveryRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveryRuns");
        }
    }
}
