using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AtlasNOC.Web.Controllers.Api;

/// <summary>API de suscriptores/CPE (inventario WISP). Lectura subscribers.read; alta subscribers.write.</summary>
[ApiController]
[Route("api/subscribers")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey")]
[EnableRateLimiting("api")]
public class SubscribersApiController : ControllerBase
{
    private readonly ISubscriberService _subscribers;
    private readonly IServiceEndpointService _endpoints;
    private readonly IAuditService _audit;

    public SubscribersApiController(ISubscriberService subscribers, IServiceEndpointService endpoints, IAuditService audit)
    {
        _subscribers = subscribers;
        _endpoints = endpoints;
        _audit = audit;
    }

    [HttpGet]
    [Authorize(Policy = ApiScopes.SubscribersRead)]
    public async Task<ActionResult<IReadOnlyList<SubscriberDto>>> List(CancellationToken ct)
        => Ok(await _subscribers.ListSubscribersAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApiScopes.SubscribersRead)]
    public async Task<ActionResult<SubscriberDto>> Get(Guid id, CancellationToken ct)
    {
        var subscriber = await _subscribers.GetSubscriberAsync(id, ct);
        return subscriber is null ? NotFound() : Ok(subscriber);
    }

    [HttpGet("{id:guid}/endpoints")]
    [Authorize(Policy = ApiScopes.SubscribersRead)]
    public async Task<ActionResult<IReadOnlyList<ServiceEndpointDto>>> ListEndpoints(Guid id, CancellationToken ct)
        => Ok(await _endpoints.ListEndpointsAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = ApiScopes.SubscribersWrite)]
    public async Task<ActionResult<SubscriberDto>> Create(CreateSubscriberRequest request, CancellationToken ct)
    {
        var subscriber = await _subscribers.CreateSubscriberAsync(request, ct);
        await _audit.RecordAsync("Subscriber", "Create", ApiActor.Id(User), ApiActor.Name(User),
            ApiActor.Role(User), subscriber.Id.ToString(), "Subscriber", ct);
        return CreatedAtAction(nameof(Get), new { id = subscriber.Id }, subscriber);
    }

    [HttpPost("{id:guid}/endpoints")]
    [Authorize(Policy = ApiScopes.SubscribersWrite)]
    public async Task<ActionResult<ServiceEndpointDto>> AssociateEndpoint(Guid id, CreateServiceEndpointRequest request, CancellationToken ct)
    {
        if (request.SubscriberId != id) return BadRequest(new ProblemDetails
        {
            Title = "El suscriptor no coincide con la ruta.",
            Status = 400,
        });

        var endpoint = await _endpoints.CreateEndpointAsync(request, ct);
        if (endpoint is null) return NotFound();

        await _audit.RecordAsync("ServiceEndpoint", "Associate", ApiActor.Id(User), ApiActor.Name(User),
            ApiActor.Role(User), endpoint.Id.ToString(), "ServiceEndpoint", ct);
        return Created("/api/subscribers/" + id + "/endpoints", endpoint);
    }
}