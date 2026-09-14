namespace AtlasNOC.Application.Services;

public sealed record PaymentRequest(Guid CustomerId, Guid? ServiceId, decimal Amount, string Reference);
public sealed record PaymentResult(bool Accepted, string Reference, string Message);
public interface IPaymentProvider { string ProviderKey { get; } Task<PaymentResult> CaptureAsync(PaymentRequest request, CancellationToken ct = default); }

public sealed record NetworkDiagnosticResult(string Status, string Summary, IReadOnlyList<string> Evidence, IReadOnlyList<Guid> AffectedServices);
public interface INetworkDiagnosticService { Task<NetworkDiagnosticResult> DiagnoseCustomerAsync(Guid customerId, CancellationToken ct = default); }

public sealed record NetworkImpactResult(int Devices, int Services, int Sites, bool Complete, string Summary);
public interface INetworkImpactService { Task<NetworkImpactResult> PreviewAsync(Guid deviceId, CancellationToken ct = default); }

public sealed record NotificationMessage(string Subject, string Body, string Recipient);
public interface INotificationProvider { string ProviderKey { get; } Task<bool> SendAsync(NotificationMessage message, CancellationToken ct = default); }

public sealed record TechnicianRouteStop(Guid VisitId, DateTime EtaUtc, int DurationMinutes);
public interface ITechnicianRoutePlanner { Task<IReadOnlyList<TechnicianRouteStop>> PlanAsync(IReadOnlyList<Guid> visitIds, CancellationToken ct = default); }

public interface IWispOperationsService
{
    Task<IReadOnlyList<OperationsDeviceDto>> ListOperationalClientsAsync(CancellationToken ct = default);
}

public sealed record NetworkActionResult(bool Succeeded, string Message, string? Evidence);
public enum NetworkActionRisk { ReadOnly, Low, Medium, High }
public sealed record NetworkActionPreview(Guid DeviceId, Application.Devices.DeviceAction Action, bool Supported,
    string CredentialState, NetworkActionRisk RiskLevel, NetworkImpactResult Impact,
    bool RequiresConfirmation, bool RequiresBackup, string TruthState, IReadOnlyList<string> Warnings);
public interface INetworkActionService
{
    Task<NetworkActionPreview> PreviewAsync(Guid deviceId, Application.Devices.DeviceAction action, string? interfaceName,
        CancellationToken ct = default);
    Task<NetworkActionResult> ExecuteAsync(Guid deviceId, Application.Devices.DeviceAction action, string? interfaceName, string? description, string actor, CancellationToken ct = default);
}
