using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasNOC.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTopologyForeignKeysAndCanonicalLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NetworkLinks_AInterfaceId",
                table: "NetworkLinks");

            // Normaliza datos históricos antes de crear la restricción. El JOIN
            // derivado evita la evaluación izquierda-a-derecha de SET en MySQL.
            migrationBuilder.Sql(@"
UPDATE NetworkLinks AS link
JOIN (
    SELECT * FROM (
        SELECT Id,
               LEAST(AInterfaceId, BInterfaceId) AS MinInterfaceId,
               GREATEST(AInterfaceId, BInterfaceId) AS MaxInterfaceId
        FROM NetworkLinks
    ) AS snapshot
) AS normalized ON normalized.Id = link.Id
SET link.AInterfaceId = normalized.MinInterfaceId,
    link.BInterfaceId = normalized.MaxInterfaceId;");

            // Conserva en el registro más antiguo la evidencia más fuerte antes
            // de retirar duplicados A-B/B-A preexistentes.
            migrationBuilder.Sql(@"
UPDATE NetworkLinks AS winner
JOIN (
    SELECT AInterfaceId, BInterfaceId,
           MAX(Confidence) AS MaxConfidence,
           MAX(LastSeenAtUtc) AS LatestSeen,
           MAX(IsConfirmed) AS AnyConfirmed,
           MAX(IsManual) AS AnyManual
    FROM NetworkLinks
    GROUP BY AInterfaceId, BInterfaceId
) AS evidence
  ON evidence.AInterfaceId = winner.AInterfaceId
 AND evidence.BInterfaceId = winner.BInterfaceId
SET winner.Confidence = evidence.MaxConfidence,
    winner.LastSeenAtUtc = evidence.LatestSeen,
    winner.IsConfirmed = evidence.AnyConfirmed,
    winner.IsManual = evidence.AnyManual;");
            migrationBuilder.Sql(@"
DELETE duplicateLink
FROM NetworkLinks AS duplicateLink
JOIN NetworkLinks AS keeper
  ON keeper.AInterfaceId = duplicateLink.AInterfaceId
 AND keeper.BInterfaceId = duplicateLink.BInterfaceId
 AND keeper.Id < duplicateLink.Id;");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_OrganizationId",
                table: "Subscribers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_SiteId",
                table: "Subscribers",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_OrganizationId",
                table: "Sites",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_ParentSiteId",
                table: "Sites",
                column: "ParentSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceEndpoints_DeviceId",
                table: "ServiceEndpoints",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceEndpoints_SubscriberId",
                table: "ServiceEndpoints",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_RadioSectors_DeviceId",
                table: "RadioSectors",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkLinks_AInterfaceId_BInterfaceId",
                table: "NetworkLinks",
                columns: new[] { "AInterfaceId", "BInterfaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NeighborObservations_LocalDeviceId",
                table: "NeighborObservations",
                column: "LocalDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborObservations_LocalInterfaceId",
                table: "NeighborObservations",
                column: "LocalInterfaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SiteId",
                table: "Devices",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCapabilities_DeviceId",
                table: "DeviceCapabilities",
                column: "DeviceId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceCapabilities_Devices_DeviceId",
                table: "DeviceCapabilities",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceInterfaces_Devices_DeviceId",
                table: "DeviceInterfaces",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Sites_SiteId",
                table: "Devices",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborObservations_DeviceInterfaces_LocalInterfaceId",
                table: "NeighborObservations",
                column: "LocalInterfaceId",
                principalTable: "DeviceInterfaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborObservations_Devices_LocalDeviceId",
                table: "NeighborObservations",
                column: "LocalDeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkLinks_DeviceInterfaces_AInterfaceId",
                table: "NetworkLinks",
                column: "AInterfaceId",
                principalTable: "DeviceInterfaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkLinks_DeviceInterfaces_BInterfaceId",
                table: "NetworkLinks",
                column: "BInterfaceId",
                principalTable: "DeviceInterfaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RadioSectors_Devices_DeviceId",
                table: "RadioSectors",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceEndpoints_Devices_DeviceId",
                table: "ServiceEndpoints",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceEndpoints_Subscribers_SubscriberId",
                table: "ServiceEndpoints",
                column: "SubscriberId",
                principalTable: "Subscribers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sites_Organizations_OrganizationId",
                table: "Sites",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sites_Sites_ParentSiteId",
                table: "Sites",
                column: "ParentSiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscribers_Organizations_OrganizationId",
                table: "Subscribers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscribers_Sites_SiteId",
                table: "Subscribers",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WirelessAssociations_Devices_ApDeviceId",
                table: "WirelessAssociations",
                column: "ApDeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WirelessAssociations_Devices_CpeDeviceId",
                table: "WirelessAssociations",
                column: "CpeDeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceCapabilities_Devices_DeviceId",
                table: "DeviceCapabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceInterfaces_Devices_DeviceId",
                table: "DeviceInterfaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Sites_SiteId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborObservations_DeviceInterfaces_LocalInterfaceId",
                table: "NeighborObservations");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborObservations_Devices_LocalDeviceId",
                table: "NeighborObservations");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkLinks_DeviceInterfaces_AInterfaceId",
                table: "NetworkLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkLinks_DeviceInterfaces_BInterfaceId",
                table: "NetworkLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_RadioSectors_Devices_DeviceId",
                table: "RadioSectors");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceEndpoints_Devices_DeviceId",
                table: "ServiceEndpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceEndpoints_Subscribers_SubscriberId",
                table: "ServiceEndpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_Sites_Organizations_OrganizationId",
                table: "Sites");

            migrationBuilder.DropForeignKey(
                name: "FK_Sites_Sites_ParentSiteId",
                table: "Sites");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscribers_Organizations_OrganizationId",
                table: "Subscribers");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscribers_Sites_SiteId",
                table: "Subscribers");

            migrationBuilder.DropForeignKey(
                name: "FK_WirelessAssociations_Devices_ApDeviceId",
                table: "WirelessAssociations");

            migrationBuilder.DropForeignKey(
                name: "FK_WirelessAssociations_Devices_CpeDeviceId",
                table: "WirelessAssociations");

            migrationBuilder.DropIndex(
                name: "IX_Subscribers_OrganizationId",
                table: "Subscribers");

            migrationBuilder.DropIndex(
                name: "IX_Subscribers_SiteId",
                table: "Subscribers");

            migrationBuilder.DropIndex(
                name: "IX_Sites_OrganizationId",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Sites_ParentSiteId",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_ServiceEndpoints_DeviceId",
                table: "ServiceEndpoints");

            migrationBuilder.DropIndex(
                name: "IX_ServiceEndpoints_SubscriberId",
                table: "ServiceEndpoints");

            migrationBuilder.DropIndex(
                name: "IX_RadioSectors_DeviceId",
                table: "RadioSectors");

            migrationBuilder.DropIndex(
                name: "IX_NetworkLinks_AInterfaceId_BInterfaceId",
                table: "NetworkLinks");

            migrationBuilder.DropIndex(
                name: "IX_NeighborObservations_LocalDeviceId",
                table: "NeighborObservations");

            migrationBuilder.DropIndex(
                name: "IX_NeighborObservations_LocalInterfaceId",
                table: "NeighborObservations");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SiteId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_DeviceCapabilities_DeviceId",
                table: "DeviceCapabilities");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkLinks_AInterfaceId",
                table: "NetworkLinks",
                column: "AInterfaceId");
        }
    }
}
