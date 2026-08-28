using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddCveRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CveRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CveId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceIdentifier = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    VulnStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CvssVersion = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CvssBaseScore = table.Column<double>(type: "double", nullable: true),
                    CvssBaseSeverity = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CvssVectorString = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExploitabilityScore = table.Column<double>(type: "double", nullable: true),
                    ImpactScore = table.Column<double>(type: "double", nullable: true),
                    Keywords = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    References = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Weaknesses = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Configurations = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CveRecords", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CveRecords_CveId",
                table: "CveRecords",
                column: "CveId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CveRecords_CvssBaseSeverity",
                table: "CveRecords",
                column: "CvssBaseSeverity");

            migrationBuilder.CreateIndex(
                name: "IX_CveRecords_Keywords",
                table: "CveRecords",
                column: "Keywords");

            migrationBuilder.CreateIndex(
                name: "IX_CveRecords_LastModifiedDate",
                table: "CveRecords",
                column: "LastModifiedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CveRecords");
        }
    }
}
