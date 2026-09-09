using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Lectura abierta a operadores y read-only; escritura restringida por acción.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class SubscribersController : Controller
{
    private readonly ISubscriberService _subscribers;
    private readonly IServiceEndpointService _endpoints;
    private readonly IDeviceService _devices;
    private readonly ISiteService _sites;
    private readonly IAuditService _audit;

    public SubscribersController(ISubscriberService subscribers, IServiceEndpointService endpoints,
        IDeviceService devices, ISiteService sites, IAuditService audit)
    {
        _subscribers = subscribers;
        _endpoints = endpoints;
        _devices = devices;
        _sites = sites;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _subscribers.ListSubscribersAsync());

    [HttpGet("subscribers/{id}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var subscriber = await _subscribers.GetSubscriberAsync(id);
        if (subscriber is null) return NotFound();
        ViewBag.Endpoints = await _endpoints.ListEndpointsAsync(id);
        ViewBag.Devices = await _devices.ListDevicesAsync();
        return View(subscriber);
    }

    [HttpGet("subscribers/create")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    public async Task<IActionResult> Create()
    {
        ViewBag.Sites = await _sites.ListSitesAsync();
        return View();
    }

    [HttpPost("subscribers/create")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSubscriberRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Sites = await _sites.ListSitesAsync();
            return View(request);
        }
        var subscriber = await _subscribers.CreateSubscriberAsync(request);
        await _audit.RecordAsync("Subscriber", "Create", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            ActionRole(), subscriber.Id.ToString(), "Subscriber");
        return RedirectToAction(nameof(Details), new { id = subscriber.Id });
    }

    [HttpGet("subscribers/{id}/edit")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var subscriber = await _subscribers.GetSubscriberAsync(id);
        if (subscriber is null) return NotFound();
        ViewBag.Sites = await _sites.ListSitesAsync();
        return View(new CreateSubscriberRequest(subscriber.Name, subscriber.SiteId));
    }

    [HttpPost("subscribers/{id}/edit")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CreateSubscriberRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Sites = await _sites.ListSitesAsync();
            return View(request);
        }
        await _subscribers.UpdateSubscriberAsync(id, request);
        await _audit.RecordAsync("Subscriber", "Edit", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            ActionRole(), id.ToString(), "Subscriber");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("subscribers/{id}/endpoints")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssociateEndpoint(Guid id, CreateServiceEndpointRequest request)
    {
        if (request.SubscriberId != id)
            ModelState.AddModelError(string.Empty, "El suscriptor no coincide con el endpoint.");
        if (!ModelState.IsValid) return RedirectToAction(nameof(Details), new { id });

        var endpoint = await _endpoints.CreateEndpointAsync(request);
        if (endpoint is null)
        {
            ModelState.AddModelError(string.Empty, "Suscriptor o CPE/device no existen.");
            return RedirectToAction(nameof(Details), new { id });
        }
        await _audit.RecordAsync("ServiceEndpoint", "Associate", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            ActionRole(), endpoint.Id.ToString(), "ServiceEndpoint");
        return RedirectToAction(nameof(Details), new { id });
    }

    private string ActionRole()
        => User.IsInRole(ApplicationRole.Administrator) ? ApplicationRole.Administrator : ApplicationRole.NocOperator;
}