using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Repositories;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.ValueObjects;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Infrastructure.Services;

public class SubscriberService : ISubscriberService
{
    private readonly ISubscriberRepository _subscribers;
    private readonly AtlasNOCDbContext _context;

    public SubscriberService(ISubscriberRepository subscribers, AtlasNOCDbContext context)
    {
        _subscribers = subscribers;
        _context = context;
    }

    public async Task<IReadOnlyList<SubscriberDto>> ListSubscribersAsync(CancellationToken ct = default)
    {
        var subscribers = await _subscribers.ListAsync(ct);
        var endpointCounts = await _context.ServiceEndpoints
            .GroupBy(e => e.SubscriberId)
            .Select(g => new { SubscriberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubscriberId, x => x.Count, ct);

        return subscribers.Select(s => new SubscriberDto(s.Id, s.Name, s.SiteId?.Value, s.IsActive,
            s.CreatedAtUtc, endpointCounts.TryGetValue(s.Id, out var c) ? c : 0)).ToList();
    }

    public async Task<SubscriberDto?> GetSubscriberAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _subscribers.GetByIdAsync(id, ct);
        if (s is null) return null;
        var count = await _context.ServiceEndpoints.CountAsync(e => e.SubscriberId == id, ct);
        return new SubscriberDto(s.Id, s.Name, s.SiteId?.Value, s.IsActive, s.CreatedAtUtc, count);
    }

    public async Task<SubscriberDto> CreateSubscriberAsync(CreateSubscriberRequest request, CancellationToken ct = default)
    {
        var org = await _context.Organizations.FirstOrDefaultAsync(ct);
        var orgId = org?.Id ?? OrganizationId.New();

        var subscriber = new Subscriber(orgId, request.Name,
            request.SiteId.HasValue ? SiteId.From(request.SiteId.Value) : null);

        await _subscribers.AddAsync(subscriber, ct);
        await _context.SaveChangesAsync(ct);
        return new SubscriberDto(subscriber.Id, subscriber.Name, subscriber.SiteId?.Value,
            subscriber.IsActive, subscriber.CreatedAtUtc, 0);
    }

    public async Task<SubscriberDto?> UpdateSubscriberAsync(Guid id, CreateSubscriberRequest request, CancellationToken ct = default)
    {
        var subscriber = await _subscribers.GetByIdAsync(id, ct);
        if (subscriber is null) return null;

        subscriber.Update(request.Name,
            request.SiteId.HasValue ? SiteId.From(request.SiteId.Value) : null);
        await _subscribers.UpdateAsync(subscriber, ct);
        await _context.SaveChangesAsync(ct);

        var count = await _context.ServiceEndpoints.CountAsync(e => e.SubscriberId == id, ct);
        return new SubscriberDto(subscriber.Id, subscriber.Name, subscriber.SiteId?.Value,
            subscriber.IsActive, subscriber.CreatedAtUtc, count);
    }
}

public class ServiceEndpointService : IServiceEndpointService
{
    private readonly IServiceEndpointRepository _endpoints;
    private readonly ISubscriberRepository _subscribers;
    private readonly IDeviceRepository _devices;
    private readonly AtlasNOCDbContext _context;

    public ServiceEndpointService(IServiceEndpointRepository endpoints,
        ISubscriberRepository subscribers, IDeviceRepository devices, AtlasNOCDbContext context)
    {
        _endpoints = endpoints;
        _subscribers = subscribers;
        _devices = devices;
        _context = context;
    }

    public async Task<IReadOnlyList<ServiceEndpointDto>> ListEndpointsAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var endpoints = await _endpoints.ListBySubscriberAsync(subscriberId, ct);
        return endpoints.Select(e => new ServiceEndpointDto(e.Id, e.SubscriberId, e.DeviceId.Value,
            e.Description, e.IsActive, e.CreatedAtUtc)).ToList();
    }

    public async Task<ServiceEndpointDto?> CreateEndpointAsync(CreateServiceEndpointRequest request, CancellationToken ct = default)
    {
        // Validación: subscriber y device deben existir antes de asociar.
        var subscriber = await _subscribers.GetByIdAsync(request.SubscriberId, ct);
        if (subscriber is null) return null;
        var device = await _devices.GetByIdAsync(request.DeviceId, ct);
        if (device is null) return null;

        var endpoint = new ServiceEndpoint(request.SubscriberId,
            DeviceId.From(request.DeviceId), request.Description);

        await _endpoints.AddAsync(endpoint, ct);
        await _context.SaveChangesAsync(ct);
        return new ServiceEndpointDto(endpoint.Id, endpoint.SubscriberId, endpoint.DeviceId.Value,
            endpoint.Description, endpoint.IsActive, endpoint.CreatedAtUtc);
    }

    public async Task<bool> DeactivateEndpointAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await _endpoints.GetByIdAsync(id, ct);
        if (endpoint is null) return false;

        endpoint.Deactivate();
        await _endpoints.UpdateAsync(endpoint, ct);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}