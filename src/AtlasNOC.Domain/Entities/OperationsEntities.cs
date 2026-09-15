namespace AtlasNOC.Domain.Entities;

public enum CustomerStatus { Active, Review, Suspended, Cancelled }
public enum ServiceStatus { Pending, Active, Suspended, Cancelled }
public enum LedgerEntryType { Charge, Payment, Credit, Adjustment }
public enum TicketStatus { Open, InProgress, Resolved, Closed }
public enum AssetStatus { UnknownDetected, Stock, Reserved, Assigned, Recovered, PendingInspection, Tested, Repair, Damaged, Retired }
public enum CoverageStatus { Available, ProbablyAvailable, RequiresFieldValidation, NoCoverageConfirmed, CapacityLimited, Saturated, Planned }
public enum PromiseStatus { Active, Fulfilled, Defaulted, Cancelled }
public enum ProspectStatus { New, CoverageChecked, Won, Lost }
public enum CpeAuthorizationStatus { Pending, Authorized, Rejected, FraudReview, Replaced }
public enum ContractStatus { Draft, Accepted, Cancelled }
public enum InstallationStatus { Planned, Scheduled, InProgress, Completed, Cancelled }
public enum SupportInteractionChannel { Phone, Chat, Email, Field }
public enum ServiceCreditStatus { Suggested, Approved, Applied, Rejected }
public enum ProvisioningStatus { NotRequested, Requested, Provisioning, Applied, Failed, PartiallyApplied, Unsupported }

public sealed class PaymentReceipt
{
    private PaymentReceipt() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public Guid AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public DateTime IssuedAtUtc { get; private set; } = DateTime.UtcNow;
    public PaymentReceipt(Guid customerId, Guid accountId, decimal amount, string reference) { if (amount <= 0 || string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Comprobante inválido."); CustomerId = customerId; AccountId = accountId; Amount = amount; Reference = reference.Trim(); }
}

public sealed class SupportInteraction
{
    private SupportInteraction() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TicketId { get; private set; }
    public SupportInteractionChannel Channel { get; private set; }
    public string Symptoms { get; private set; } = string.Empty;
    public string Diagnosis { get; private set; } = string.Empty;
    public string Actions { get; private set; } = string.Empty;
    public string Operator { get; private set; } = string.Empty;
    public string Result { get; private set; } = string.Empty;
    public int DurationMinutes { get; private set; }
    public DateTime StartedAtUtc { get; private set; } = DateTime.UtcNow;
    public Guid? RootIncidentId { get; private set; }
    public SupportInteraction(Guid ticketId, SupportInteractionChannel channel, string symptoms, string diagnosis, string actions, string @operator, string result, int durationMinutes, Guid? rootIncidentId = null) { if (durationMinutes < 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes)); TicketId = ticketId; Channel = channel; Symptoms = symptoms; Diagnosis = diagnosis; Actions = actions; Operator = @operator; Result = result; DurationMinutes = durationMinutes; RootIncidentId = rootIncidentId; }
}

public sealed class ServiceCredit
{
    private ServiceCredit() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public Guid? IncidentId { get; private set; }
    public DateTime FromUtc { get; private set; }
    public DateTime ToUtc { get; private set; }
    public decimal SuggestedAmount { get; private set; }
    public ServiceCreditStatus Status { get; private set; } = ServiceCreditStatus.Suggested;
    public string Reason { get; private set; } = string.Empty;
    public ServiceCredit(Guid customerId, Guid? incidentId, DateTime fromUtc, DateTime toUtc, decimal suggestedAmount, string reason) { if (toUtc <= fromUtc || suggestedAmount < 0) throw new ArgumentException("Crédito inválido."); CustomerId = customerId; IncidentId = incidentId; FromUtc = fromUtc; ToUtc = toUtc; SuggestedAmount = suggestedAmount; Reason = reason; }
    public void SetStatus(ServiceCreditStatus status) => Status = status;
}

public sealed class ServiceContract
{
    private ServiceContract() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public Guid ServiceId { get; private set; }
    public string Terms { get; private set; } = string.Empty;
    public ContractStatus Status { get; private set; } = ContractStatus.Draft;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? AcceptedAtUtc { get; private set; }
    public string? AcceptedBy { get; private set; }
    public ServiceContract(Guid customerId, Guid serviceId, string terms) { if (string.IsNullOrWhiteSpace(terms)) throw new ArgumentException("Términos obligatorios."); CustomerId = customerId; ServiceId = serviceId; Terms = terms.Trim(); }
    public void Accept(string actor) { Status = ContractStatus.Accepted; AcceptedAtUtc = DateTime.UtcNow; AcceptedBy = actor; }
    public void Cancel() => Status = ContractStatus.Cancelled;
}

public sealed class InstallationOrder
{
    private InstallationOrder() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerServiceId { get; private set; }
    public bool OutsideCity { get; private set; }
    public decimal InstallationFee { get; private set; }
    public decimal RouterFee { get; private set; }
    public InstallationStatus Status { get; private set; } = InstallationStatus.Planned;
    public DateTime? ScheduledAtUtc { get; private set; }
    public InstallationOrder(Guid serviceId, bool outsideCity, decimal installationFee, decimal routerFee) { if (installationFee < 0 || routerFee < 0) throw new ArgumentOutOfRangeException(); CustomerServiceId = serviceId; OutsideCity = outsideCity; InstallationFee = installationFee; RouterFee = routerFee; }
    public void Schedule(DateTime atUtc) { ScheduledAtUtc = atUtc; Status = InstallationStatus.Scheduled; }
    public void Complete() => Status = InstallationStatus.Completed;
}

public sealed class CpeAuthorizationCase
{
    private CpeAuthorizationCase() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? CustomerServiceId { get; private set; }
    public string MacAddress { get; private set; } = string.Empty;
    public string? AccessPoint { get; private set; }
    public string? IpAddress { get; private set; }
    public double? Rssi { get; private set; }
    public double? Snr { get; private set; }
    public CpeAuthorizationStatus Status { get; private set; } = CpeAuthorizationStatus.Pending;
    public ProvisioningStatus Provisioning { get; private set; } = ProvisioningStatus.NotRequested;
    public string? EnforcementEvidence { get; private set; }
    public string? DecisionReason { get; private set; }
    public string? DecidedBy { get; private set; }
    public DateTime DetectedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; private set; }
    public CpeAuthorizationCase(string macAddress, string? accessPoint, string? ipAddress, double? rssi, double? snr, Guid? customerServiceId = null) { if (string.IsNullOrWhiteSpace(macAddress)) throw new ArgumentException("MAC obligatoria."); MacAddress = macAddress.Trim().ToUpperInvariant(); AccessPoint = accessPoint?.Trim(); IpAddress = ipAddress?.Trim(); Rssi = rssi; Snr = snr; CustomerServiceId = customerServiceId; }
    public void Decide(CpeAuthorizationStatus status, string reason, string actor) { if (status is not (CpeAuthorizationStatus.Authorized or CpeAuthorizationStatus.Rejected or CpeAuthorizationStatus.FraudReview or CpeAuthorizationStatus.Replaced)) throw new ArgumentException("Decisión inválida."); Status = status; DecisionReason = reason; DecidedBy = actor; DecidedAtUtc = DateTime.UtcNow; }
    public void SetProvisioning(ProvisioningStatus status, string? evidence = null) { Provisioning = status; EnforcementEvidence = evidence; }
}

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
    { if (deviceId == Guid.Empty || revision < 1 || string.IsNullOrWhiteSpace(beforeHash) || string.IsNullOrWhiteSpace(afterHash) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Revisión de configuración incompleta."); DeviceId = deviceId; Revision = revision; BeforeHash = beforeHash.Trim(); AfterHash = afterHash.Trim(); Source = source.Trim(); Reason = reason.Trim(); Actor = actor.Trim(); }
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
    public ProvisioningStatus Provisioning { get; private set; } = ProvisioningStatus.NotRequested;
    public string? ProvisioningEvidence { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public CustomerService(Guid customerId, Guid planId, string address) { CustomerId = customerId; PlanId = planId; ServiceAddress = address; }
    public void Activate() { if (Status == ServiceStatus.Cancelled) throw new InvalidOperationException("Un servicio cancelado no puede reactivarse."); Status = ServiceStatus.Active; ActivatedAtUtc = DateTime.UtcNow; }
    public void Suspend() => Status = ServiceStatus.Suspended;
    public void Reconnect() { if (Status == ServiceStatus.Cancelled) throw new InvalidOperationException("Un servicio cancelado no puede reconectarse."); Status = ServiceStatus.Active; }
    public void ChangePlan(Guid planId) { if (planId == Guid.Empty) throw new ArgumentException("Plan inválido."); PlanId = planId; }
    public void Cancel() => Status = ServiceStatus.Cancelled;
    public void SetProvisioning(ProvisioningStatus status, string? evidence = null) { Provisioning = status; ProvisioningEvidence = evidence; }
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
    public DateTime? DueAtUtc { get; private set; }
    public string? Period { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public BillingEntry(Guid accountId, LedgerEntryType type, decimal amount, string description, DateTime? dueAtUtc = null, string? period = null, string? idempotencyKey = null) { AccountId = accountId; Type = type; Amount = amount; Description = description; DueAtUtc = dueAtUtc; Period = period; IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(); }
    public bool IsOverdue(DateTime nowUtc, int graceDays) => Type == LedgerEntryType.Charge && DueAtUtc.HasValue && nowUtc > DueAtUtc.Value.AddDays(graceDays);
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
    public SupportTicket(Guid customerId, string title, string? description = null) { if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("El título es obligatorio."); CustomerId = customerId; Title = title.Trim(); Description = description?.Trim(); }
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
    public void Recover() => Status = AssetStatus.Recovered;
    public void MarkInspected(bool passed) => Status = passed ? AssetStatus.Tested : AssetStatus.Repair;
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
    public int? ActualMinutes { get; private set; }
    public int? TravelMinutes { get; private set; }
    public string? Result { get; private set; }
    public TechnicianVisit(Guid customerId, DateTime scheduledAtUtc, string workType, int estimatedMinutes) { if (string.IsNullOrWhiteSpace(workType) || estimatedMinutes <= 0) throw new ArgumentException("Trabajo y duración estimada son obligatorios."); CustomerId = customerId; ScheduledAtUtc = scheduledAtUtc; WorkType = workType.Trim(); EstimatedMinutes = estimatedMinutes; }
    public void Complete(int actualMinutes, int travelMinutes, string result) { if (actualMinutes < 0 || travelMinutes < 0) throw new ArgumentOutOfRangeException(); ActualMinutes = actualMinutes; TravelMinutes = travelMinutes; Result = result; }
}
