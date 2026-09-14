using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AtlasNOC.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AtlasNOCDbContext))]
[Migration("20260914150000_AddOperationalExtensions")]
public partial class AddOperationalExtensions : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("CREATE TABLE IF NOT EXISTS ConfigurationRevisions (Id char(36) NOT NULL, DeviceId char(36) NOT NULL, Revision int NOT NULL, BeforeHash varchar(256) NOT NULL, AfterHash varchar(256) NOT NULL, Source varchar(100) NOT NULL, Reason varchar(500) NOT NULL, Actor varchar(200) NOT NULL, KnownGood bit NOT NULL, CreatedAtUtc datetime(6) NOT NULL, PRIMARY KEY (Id), UNIQUE KEY UX_ConfigurationRevisions_Device_Revision (DeviceId, Revision))");
        m.Sql("CREATE TABLE IF NOT EXISTS Prospects (Id char(36) NOT NULL, Name varchar(200) NOT NULL, Address varchar(500) NOT NULL, Phone varchar(64) NULL, Status int NOT NULL, CreatedAtUtc datetime(6) NOT NULL, PRIMARY KEY (Id))");
        m.Sql("CREATE TABLE IF NOT EXISTS PaymentPromises (Id char(36) NOT NULL, CustomerId char(36) NOT NULL, Amount decimal(18,2) NOT NULL, Remaining decimal(18,2) NOT NULL, PromisedAtUtc datetime(6) NOT NULL, ExpiresAtUtc datetime(6) NOT NULL, AuthorizedBy varchar(200) NOT NULL, Conditions varchar(1000) NOT NULL, Status int NOT NULL, PRIMARY KEY (Id), CONSTRAINT FK_PaymentPromises_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id))");
        m.Sql("CREATE TABLE IF NOT EXISTS CpeAuthorizationCases (Id char(36) NOT NULL, CustomerServiceId char(36) NULL, MacAddress varchar(32) NOT NULL, AccessPoint varchar(200) NULL, IpAddress varchar(64) NULL, Rssi double NULL, Snr double NULL, Status int NOT NULL, DecisionReason varchar(500) NULL, DecidedBy varchar(200) NULL, DetectedAtUtc datetime(6) NOT NULL, DecidedAtUtc datetime(6) NULL, PRIMARY KEY (Id), CONSTRAINT FK_CpeAuthorizationCases_Services FOREIGN KEY (CustomerServiceId) REFERENCES CustomerServices(Id) ON DELETE SET NULL)");
        m.Sql("CREATE TABLE IF NOT EXISTS ServiceContracts (Id char(36) NOT NULL, CustomerId char(36) NOT NULL, ServiceId char(36) NOT NULL, Terms varchar(8000) NOT NULL, Status int NOT NULL, CreatedAtUtc datetime(6) NOT NULL, AcceptedAtUtc datetime(6) NULL, AcceptedBy varchar(200) NULL, PRIMARY KEY (Id), CONSTRAINT FK_ServiceContracts_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id), CONSTRAINT FK_ServiceContracts_Services FOREIGN KEY (ServiceId) REFERENCES CustomerServices(Id))");
        m.Sql("CREATE TABLE IF NOT EXISTS InstallationOrders (Id char(36) NOT NULL, CustomerServiceId char(36) NOT NULL, OutsideCity bit NOT NULL, InstallationFee decimal(18,2) NOT NULL, RouterFee decimal(18,2) NOT NULL, Status int NOT NULL, ScheduledAtUtc datetime(6) NULL, PRIMARY KEY (Id), CONSTRAINT FK_InstallationOrders_Services FOREIGN KEY (CustomerServiceId) REFERENCES CustomerServices(Id))");
        m.Sql("CREATE TABLE IF NOT EXISTS SupportInteractions (Id char(36) NOT NULL, TicketId char(36) NOT NULL, Channel int NOT NULL, Symptoms varchar(4000) NOT NULL, Diagnosis varchar(4000) NOT NULL, Actions varchar(4000) NOT NULL, Operator varchar(200) NOT NULL, Result varchar(1000) NOT NULL, DurationMinutes int NOT NULL, StartedAtUtc datetime(6) NOT NULL, RootIncidentId char(36) NULL, PRIMARY KEY (Id), CONSTRAINT FK_SupportInteractions_Tickets FOREIGN KEY (TicketId) REFERENCES SupportTickets(Id))");
        m.Sql("CREATE TABLE IF NOT EXISTS ServiceCredits (Id char(36) NOT NULL, CustomerId char(36) NOT NULL, IncidentId char(36) NULL, FromUtc datetime(6) NOT NULL, ToUtc datetime(6) NOT NULL, SuggestedAmount decimal(18,2) NOT NULL, Status int NOT NULL, Reason varchar(1000) NOT NULL, PRIMARY KEY (Id), CONSTRAINT FK_ServiceCredits_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id))");
        m.Sql("CREATE TABLE IF NOT EXISTS PaymentReceipts (Id char(36) NOT NULL, CustomerId char(36) NOT NULL, AccountId char(36) NOT NULL, Amount decimal(18,2) NOT NULL, Reference varchar(200) NOT NULL, IssuedAtUtc datetime(6) NOT NULL, PRIMARY KEY (Id), UNIQUE KEY UX_PaymentReceipts_Reference (Reference), CONSTRAINT FK_PaymentReceipts_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id), CONSTRAINT FK_PaymentReceipts_Accounts FOREIGN KEY (AccountId) REFERENCES BillingAccounts(Id))");
        m.Sql("ALTER TABLE Incidents ADD COLUMN Priority int NOT NULL DEFAULT 3");
        m.Sql("ALTER TABLE Incidents ADD COLUMN PriorityChangeReason varchar(500) NULL");
        m.Sql("ALTER TABLE TechnicianVisits ADD COLUMN ActualMinutes int NULL");
        m.Sql("ALTER TABLE TechnicianVisits ADD COLUMN TravelMinutes int NULL");
        m.Sql("ALTER TABLE TechnicianVisits ADD COLUMN Result varchar(1000) NULL");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP TABLE IF EXISTS PaymentReceipts"); m.Sql("DROP TABLE IF EXISTS ServiceCredits"); m.Sql("DROP TABLE IF EXISTS SupportInteractions"); m.Sql("DROP TABLE IF EXISTS InstallationOrders"); m.Sql("DROP TABLE IF EXISTS ServiceContracts"); m.Sql("DROP TABLE IF EXISTS CpeAuthorizationCases"); m.Sql("DROP TABLE IF EXISTS PaymentPromises"); m.Sql("DROP TABLE IF EXISTS Prospects"); m.Sql("DROP TABLE IF EXISTS ConfigurationRevisions");
    }
}
