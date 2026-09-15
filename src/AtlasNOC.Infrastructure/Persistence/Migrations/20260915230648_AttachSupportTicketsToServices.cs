using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttachSupportTicketsToServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerServiceId",
                table: "SupportTickets",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CustomerServiceId",
                table: "SupportTickets",
                column: "CustomerServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportTickets_CustomerServices_CustomerServiceId",
                table: "SupportTickets",
                column: "CustomerServiceId",
                principalTable: "CustomerServices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportTickets_CustomerServices_CustomerServiceId",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_CustomerServiceId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "CustomerServiceId",
                table: "SupportTickets");
        }
    }
}
