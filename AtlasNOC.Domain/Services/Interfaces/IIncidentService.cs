using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasNOC.Domain.Entities;

namespace AtlasNOC.Domain.Services.Interfaces;

public interface IIncidentService
{
    Task<Incident> CreateAsync(string title, string description, string createdBy,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Incident>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Incident>> GetOpenAsync(CancellationToken cancellationToken = default);
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Incident> UpdateStatusToInvestigatingAsync(Guid incidentId, string modifiedBy,
        CancellationToken cancellationToken = default);
    Task<Incident> UpdateStatusToMonitoringAsync(Guid incidentId, string modifiedBy,
        CancellationToken cancellationToken = default);
    Task<Incident> ResolveAsync(Guid incidentId, string resolvedBy, string? notes = null,
        CancellationToken cancellationToken = default);
}
