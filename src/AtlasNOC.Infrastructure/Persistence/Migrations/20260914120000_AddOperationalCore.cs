using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AtlasNOC.Infrastructure.Persistence;
#nullable disable
namespace AtlasNOC.Infrastructure.Persistence.Migrations;
[DbContext(typeof(AtlasNOCDbContext))]
[Migration("20260914120000_AddOperationalCore")]
public partial class AddOperationalCore : Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.Sql("CREATE TABLE IF NOT EXISTS Customers (Id char(36) NOT NULL, ServiceCode varchar(64) NOT NULL, Name varchar(200) NOT NULL, Phone varchar(64) NULL, Email varchar(255) NULL, Status int NOT NULL, CreatedAtUtc datetime(6) NOT NULL, PRIMARY KEY (Id), UNIQUE KEY UX_Customers_ServiceCode (ServiceCode))");
  m.Sql("CREATE TABLE IF NOT EXISTS ServicePlans (Id char(36) NOT NULL, Name varchar(200) NOT NULL, MonthlyPrice decimal(18,2) NOT NULL, DownloadMbps int NOT NULL, UploadMbps int NOT NULL, IsActive bit NOT NULL, PRIMARY KEY (Id))");
  m.Sql("CREATE TABLE IF NOT EXISTS CustomerServices (Id char(36) NOT NULL, CustomerId char(36) NOT NULL, PlanId char(36) NOT NULL, ServiceAddress varchar(500) NOT NULL, Status int NOT NULL, ActivatedAtUtc datetime(6) NULL, PRIMARY KEY (Id), KEY IX_CustomerServices_CustomerId (CustomerId), CONSTRAINT FK_CustomerServices_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id), CONSTRAINT FK_CustomerServices_ServicePlans FOREIGN KEY (PlanId) REFERENCES ServicePlans(Id))");
  m.Sql("CREATE TABLE IF NOT EXISTS BillingAccounts (Id char(36) NOT NULL, CustomerId char(36) NOT NULL, Balance decimal(18,2) NOT NULL, PRIMARY KEY (Id), UNIQUE KEY UX_BillingAccounts_CustomerId (CustomerId), CONSTRAINT FK_BillingAccounts_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id))");
  m.Sql("CREATE TABLE IF NOT EXISTS BillingEntries (Id char(36) NOT NULL, AccountId char(36) NOT NULL, Type int NOT NULL, Amount decimal(18,2) NOT NULL, Description varchar(500) NOT NULL, OccurredAtUtc datetime(6) NOT NULL, PRIMARY KEY (Id), KEY IX_BillingEntries_AccountId (AccountId), CONSTRAINT FK_BillingEntries_Accounts FOREIGN KEY (AccountId) REFERENCES BillingAccounts(Id))");
  m.Sql("CREATE TABLE IF NOT EXISTS SupportTickets (Id char(36) NOT NULL, CustomerId char(36) NOT NULL, Title varchar(300) NOT NULL, Description varchar(4000) NULL, Status int NOT NULL, CreatedAtUtc datetime(6) NOT NULL, PRIMARY KEY (Id), KEY IX_SupportTickets_Status (Status), CONSTRAINT FK_SupportTickets_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id))");
  m.Sql("CREATE TABLE IF NOT EXISTS InventoryAssets (Id char(36) NOT NULL, AssetTag varchar(100) NOT NULL, SerialNumber varchar(200) NULL, MacAddress varchar(32) NULL, Type varchar(100) NOT NULL, Status int NOT NULL, CustomerServiceId char(36) NULL, PRIMARY KEY (Id), KEY IX_InventoryAssets_Serial (SerialNumber), CONSTRAINT FK_InventoryAssets_Services FOREIGN KEY (CustomerServiceId) REFERENCES CustomerServices(Id) ON DELETE SET NULL)");
  m.Sql("CREATE TABLE IF NOT EXISTS CoverageChecks (Id char(36) NOT NULL, Address varchar(500) NOT NULL, Status int NOT NULL, CapacityMbps int NULL, CheckedAtUtc datetime(6) NOT NULL, PRIMARY KEY (Id))");
  m.Sql("CREATE TABLE IF NOT EXISTS TechnicianVisits (Id char(36) NOT NULL, CustomerId char(36) NOT NULL, ScheduledAtUtc datetime(6) NOT NULL, WorkType varchar(200) NOT NULL, EstimatedMinutes int NOT NULL, PRIMARY KEY (Id), KEY IX_TechnicianVisits_Scheduled (ScheduledAtUtc), CONSTRAINT FK_TechnicianVisits_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id))");
 }
 protected override void Down(MigrationBuilder m){m.Sql("DROP TABLE IF EXISTS TechnicianVisits");m.Sql("DROP TABLE IF EXISTS CoverageChecks");m.Sql("DROP TABLE IF EXISTS InventoryAssets");m.Sql("DROP TABLE IF EXISTS SupportTickets");m.Sql("DROP TABLE IF EXISTS BillingEntries");m.Sql("DROP TABLE IF EXISTS BillingAccounts");m.Sql("DROP TABLE IF EXISTS CustomerServices");m.Sql("DROP TABLE IF EXISTS ServicePlans");m.Sql("DROP TABLE IF EXISTS Customers");}
}
