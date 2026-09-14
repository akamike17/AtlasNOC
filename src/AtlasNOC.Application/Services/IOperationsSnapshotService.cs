namespace AtlasNOC.Application.Services;

public sealed record OperationsSnapshotDto(
    DateTime GeneratedAtUtc,
    int Devices,
    int Online,
    int Offline,
    int Unknown,
    int OpenAlerts,
    int ConfirmedLinks,
    IReadOnlyList<OperationsDeviceDto> DeviceStates);

public sealed record OperationsDeviceDto(Guid Id, string Name, string Ip, int Status, bool Managed, string TruthState);

public interface IOperationsSnapshotService
{
    Task<OperationsSnapshotDto> GetAsync(CancellationToken ct = default);
}
