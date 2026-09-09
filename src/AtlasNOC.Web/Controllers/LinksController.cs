using AtlasNOC.Application.Dtos;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Lectura abierta a operadores y read-only; escritura restringida por acción.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class LinksController : Controller
{
    private readonly ILinkService _links;
    private readonly IInterfaceService _interfaces;
    private readonly IAuditService _audit;

    public LinksController(ILinkService links, IInterfaceService interfaces, IAuditService audit)
    {
        _links = links;
        _interfaces = interfaces;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _links.ListLinksAsync());

    [HttpGet("links/{id}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var link = await _links.GetLinkAsync(id);
        if (link is null) return NotFound();
        var a = await _interfaces.GetInterfaceAsync(link.AInterfaceId);
        var b = await _interfaces.GetInterfaceAsync(link.BInterfaceId);
        ViewBag.A = a;
        ViewBag.B = b;
        return View(link);
    }

    [HttpPost("links/{id}/confirm")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(Guid id)
    {
        await _links.ConfirmLinkAsync(id);
        await _audit.RecordAsync("Link", "Confirm", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            ActionRole(), id.ToString(), "NetworkLink");
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("links/{id}/reject")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id)
    {
        await _links.RejectLinkAsync(id);
        await _audit.RecordAsync("Link", "Reject", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            ActionRole(), id.ToString(), "NetworkLink");
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("links/create-manual")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    public IActionResult CreateManual() => View();

    [HttpPost("links/create-manual")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateManual(CreateManualLinkRequest request)
    {
        var link = await _links.CreateManualLinkAsync(request);
        if (link is null)
        {
            ModelState.AddModelError(string.Empty, "Las interfaces indicadas no existen o son idénticas.");
            return View(request);
        }
        await _audit.RecordAsync("Link", "CreateManual", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            ActionRole(), link.Id.ToString(), "NetworkLink");
        return RedirectToAction(nameof(Detail), new { id = link.Id });
    }

    [HttpPost("links/{id}/edit-metadata")]
    [Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMetadata(UpdateLinkMetadataRequest request)
    {
        var link = await _links.UpdateLinkMetadataAsync(request);
        if (link is null) return NotFound();
        await _audit.RecordAsync("Link", "EditMetadata", User.Identity?.Name ?? "", User.Identity?.Name ?? "",
            ActionRole(), link.Id.ToString(), "NetworkLink");
        return RedirectToAction(nameof(Detail), new { id = link.Id });
    }

    private string ActionRole()
        => User.IsInRole(ApplicationRole.Administrator) ? ApplicationRole.Administrator : ApplicationRole.NocOperator;
}