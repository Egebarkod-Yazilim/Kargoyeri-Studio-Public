using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

[Authorize(Policy = StudioRoles.CanManageTenant)]
public sealed class NotificationTemplatesController : Controller
{
    private readonly NotificationTemplateStore _templateStore;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;

    public NotificationTemplatesController(
        NotificationTemplateStore templateStore,
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext)
    {
        _templateStore = templateStore;
        _customerService = customerService;
        _workspaceContext = workspaceContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var templates = await _templateStore.GetAsync(workspace.TenantKey, cancellationToken);

        return View(new NotificationTemplatePageViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Templates = templates
                .Select(x => new NotificationTemplateEditorItemViewModel
                {
                    EventType = x.EventType,
                    Channel = x.Channel,
                    IsEnabled = x.IsEnabled,
                    SubjectTemplate = x.SubjectTemplate,
                    BodyTemplate = x.BodyTemplate
                })
                .ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(NotificationTemplatePageViewModel model, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        model.WorkspaceCode = workspace.TenantKey;
        model.WorkspaceName = workspace.Name;

        foreach (var item in model.Templates)
        {
            if (string.IsNullOrWhiteSpace(item.BodyTemplate))
            {
                ModelState.AddModelError(string.Empty, $"{item.EventType} / {item.Channel} icin mesaj govdesi bos olamaz.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        await _templateStore.SaveAsync(
            workspace.TenantKey,
            model.Templates.Select(x => new NotificationTemplateDefinition(
                x.EventType,
                x.Channel,
                x.IsEnabled,
                x.SubjectTemplate ?? string.Empty,
                x.BodyTemplate ?? string.Empty)).ToArray(),
            cancellationToken);

        StudioFlash.Success(TempData, "Bildirim sablonlari kaydedildi.");
        return RedirectToAction(nameof(Index));
    }
}
