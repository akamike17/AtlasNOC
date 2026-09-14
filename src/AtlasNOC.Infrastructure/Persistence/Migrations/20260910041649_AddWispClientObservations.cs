using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWispClientObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WispClientObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ExternalId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    AccountReference = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CpeAddress = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    SessionReference = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Source = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Confidence = table.Column<double>(type: "double", precision: 5, scale: 4, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_WispClientObservations", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.CreateIndex("IX_WispClientObservations_ExternalId_Source_ObservedAtUtc", "WispClientObservations", new[] { "ExternalId", "Source", "ObservedAtUtc" }, unique: true);
            migrationBuilder.CreateIndex("IX_WispClientObservations_SessionReference", "WispClientObservations", "SessionReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropTable(name: "WispClientObservations");
    }
}
