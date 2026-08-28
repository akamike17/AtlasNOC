using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetricSamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeviceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Success = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LatencyMs = table.Column<double>(type: "double", nullable: true),
                    AvailabilityPercent = table.Column<double>(type: "double", nullable: false),
                    InterfaceMetricsJson = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricSamples_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotificationChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Configuration = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationChannels", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MetricSamples_DeviceId_Timestamp",
                table: "MetricSamples",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricSamples_Timestamp",
                table: "MetricSamples",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannels_Name",
                table: "NotificationChannels",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetricSamples");

            migrationBuilder.DropTable(
                name: "NotificationChannels");
        }
    }
}
