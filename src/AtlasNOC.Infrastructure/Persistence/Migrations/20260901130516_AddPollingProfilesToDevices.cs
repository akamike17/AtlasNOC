using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPollingProfilesToDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WirelessIntervalSeconds",
                table: "PollingProfiles",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AlterColumn<int>(
                name: "IsResolved",
                table: "NeighborObservations",
                type: "int",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHealthPolledAtUtc",
                table: "Devices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInterfacePolledAtUtc",
                table: "Devices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWirelessPolledAtUtc",
                table: "Devices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PollingProfileId",
                table: "Devices",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_PollingProfiles_IsDefault",
                table: "PollingProfiles",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PollingProfileId",
                table: "Devices",
                column: "PollingProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_PollingProfiles_PollingProfileId",
                table: "Devices",
                column: "PollingProfileId",
                principalTable: "PollingProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_PollingProfiles_PollingProfileId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_PollingProfiles_IsDefault",
                table: "PollingProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Devices_PollingProfileId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WirelessIntervalSeconds",
                table: "PollingProfiles");

            migrationBuilder.DropColumn(
                name: "LastHealthPolledAtUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastInterfacePolledAtUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastWirelessPolledAtUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PollingProfileId",
                table: "Devices");

            migrationBuilder.AlterColumn<bool>(
                name: "IsResolved",
                table: "NeighborObservations",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
