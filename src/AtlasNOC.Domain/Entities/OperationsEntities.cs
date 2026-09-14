namespace AtlasNOC.Domain.Entities;

public enum CustomerStatus { Active, Review, Suspended, Cancelled }
public enum ServiceStatus { Pending, Active, Suspended, Cancelled }
public enum LedgerEntryType { Charge, Payment, Credit, Adjustment }
public enum TicketStatus { Open, InProgress, Resolved, Closed }
public enum AssetStatus { UnknownDetected, Stock, Reserved, Assigned, Recovered, PendingInspection, Tested, Repair, Damaged, Retired }
public enum CoverageStatus { Available, ProbablyAvailable, RequiresFieldValidation, NoCoverageConfirmed, CapacityLimited, Saturated, Planned }
public enum PromiseStatus { Active, Fulfilled, Defaulted, Cancelled }
public enum ProspectStatus { New, CoverageChecked, Won, Lost }

public sealed class Prospect
{
    private Prospect() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public ProspectStatus Status { get; private set; } = ProspectStatus.New;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public Prospect(string name, string address, string? phone = null) { if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Nombre y domicilio son obligatorios."); Name = name.Trim(); Address = address.Trim(); Phone = phone?.Trim(); }
    public void SetStatus(ProspectStatus status) => Status = status;
}

public sealed class PaymentPromise
{
    private PaymentPromise() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal Remaining { get; private set; }
    public DateTime PromisedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public string AuthorizedBy { get; private set; } = string.Empty;
    public string Conditions { get; private set; } = string.Empty;
    public PromiseStatus Status { get; private set; } = PromiseStatus.Active;
    public PaymentPromise(Guid customerId, decimal amount, DateTime promisedAtUtc, DateTime expiresAtUtc, string authorizedBy, string conditions) { if (amount <= 0 || expiresAtUtc < promisedAtUtc) throw new ArgumentException("Promesa inválida."); CustomerId = customerId; Amount = amount; Remaining = amount; PromisedAtUtc = promisedAtUtc; ExpiresAtUtc = expiresAtUtc; AuthorizedBy = authorizedBy; Conditions = conditions; }
    public void ApplyPayment(decimal amount) { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); Remaining = Math.Max(0, Remaining - amount); if (Remaining == 0) Status = PromiseStatus.Fulfilled; }
    public void Default() => Status = PromiseStatus.Defaulted;
}

public sealed class ConfigurationRevision
{
    private ConfigurationRevision() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DeviceId { get; private set; }
    public int Revision { get; private set; }
    public string BeforeHash { get; private set; } = string.Empty;
    public string AfterHash { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public bool KnownGood { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public ConfigurationRevision(Guid deviceId, int revision, string beforeHash, string afterHash, string source, string reason, string actor)
    { if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision)); DeviceId = deviceId; Revision = revision; BeforeHash = beforeHash; AfterHash = afterHash; Source = source; Reason = reason; Actor = actor; }
    public void MarkKnownGood() => KnownGood = true;
}

public sealed class Customer
{
    private Customer() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ServiceCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public CustomerStatus Status { get; private set; } = CustomerStatus.Active;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public Customer(string serviceCode, string name, string? phone = null, string? email = null)
    { if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre es obligatorio."); ServiceCode = serviceCode; Name = name.Trim(); Phone = phone; Email = email; }
    public void SetStatus(CustomerStatus status) => Status = status;
}

public sealed class ServicePlan
{
    private ServicePlan() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public decimal MonthlyPrice { get; private set; }
    public int DownloadMbps { get; private set; }
    public int UploadMbps { get; private set; }
    public bool IsActive { get; private set; } = true;
    public ServicePlan(string name, decimal monthlyPrice, int downloadMbps, int uploadMbps)
    { if (monthlyPrice < 0 || downloadMbps < 0 || uploadMbps < 0) throw new ArgumentOutOfRangeException(); Name = name; MonthlyPrice = monthlyPrice; DownloadMbps = downloadMbps; UploadMbps = uploadMbps; }
}

public sealed class CustomerService
{
    private CustomerService() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public Guid PlanId { get; private set; }
    public string ServiceAddress { get; private set; } = string.Empty;
    public ServiceStatus Status { get; private set; } = ServiceStatus.Pending;
    public DateTime? ActivatedAtUtc { get; private set; }
    public CustomerService(Guid customerId, Guid planId, string address) { CustomerId = customerId; PlanId = planId; ServiceAddress = address; }
    public void Activate() { Status = ServiceStatus.Active; ActivatedAtUtc = DateTime.UtcNow; }
    public void Suspend() => Status = ServiceStatus.Suspended;
    public void Reconnect() => Status = ServiceStatus.Active;
}

public sealed class BillingAccount
{
    private BillingAccount() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public decimal Balance { get; private set; }
    public BillingAccount(Guid customerId) { CustomerId = customerId; }
    public void Apply(LedgerEntryType type, decimal amount) { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); Balance += type == LedgerEntryType.Payment || type == LedgerEntryType.Credit ? -amount : amount; }
}

public sealed class BillingEntry
{
    private BillingEntry() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid AccountId { get; private set; }
    public LedgerEntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; } = DateTime.UtcNow;
    public BillingEntry(Guid accountId, LedgerEntryType type, decimal amount, string description) { AccountId = accountId; Type = type; Amount = amount; Description = description; }
}

public sealed class SupportTicket
{
    private SupportTicket() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public SupportTicket(Guid customerId, string title, string? description = null) { CustomerId = customerId; Title = title; Description = description; }
    public void SetStatus(TicketStatus status) => Status = status;
}

public sealed class InventoryAsset
{
    private InventoryAsset() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string AssetTag { get; private set; } = string.Empty;
    public string? SerialNumber { get; private set; }
    public string? MacAddress { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public AssetStatus Status { get; private set; } = AssetStatus.UnknownDetected;
    public Guid? CustomerServiceId { get; private set; }
    public InventoryAsset(string assetTag, string type, string? serialNumber = null, string? macAddress = null) { AssetTag = assetTag; Type = type; SerialNumber = serialNumber; MacAddress = macAddress; }
    public void Assign(Guid serviceId) { CustomerServiceId = serviceId; Status = AssetStatus.Assigned; }
    public void SetStatus(AssetStatus status) => Status = status;
}

public sealed class CoverageCheck
{
    private CoverageCheck() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Address { get; private set; } = string.Empty;
    public CoverageStatus Status { get; private set; }
    public int? CapacityMbps { get; private set; }
    public DateTime CheckedAtUtc { get; private set; } = DateTime.UtcNow;
    public CoverageCheck(string address, CoverageStatus status, int? capacityMbps = null) { Address = address; Status = status; CapacityMbps = capacityMbps; }
}

public sealed class TechnicianVisit
{
    private TechnicianVisit() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public string WorkType { get; private set; } = string.Empty;
    public int EstimatedMinutes { get; private set; }
    public TechnicianVisit(Guid customerId, DateTime scheduledAtUtc, string workType, int estimatedMinutes) { CustomerId = customerId; ScheduledAtUtc = scheduledAtUtc; WorkType = workType; EstimatedMinutes = estimatedMinutes; }
}
