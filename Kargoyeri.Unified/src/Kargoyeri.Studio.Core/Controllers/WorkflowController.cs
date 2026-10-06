using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

[Authorize(Roles = StudioRoles.Admin)]
public sealed class WorkflowController : Controller
{
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly WorkflowRuleStore _ruleStore;

    public WorkflowController(
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext,
        WorkflowRuleStore ruleStore)
    {
        _customerService = customerService;
        _workspaceContext = workspaceContext;
        _ruleStore = ruleStore;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var rules = await _ruleStore.GetAsync(workspace.TenantKey, cancellationToken);
        return View(new WorkflowRulePageViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Rules = rules
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkflowRuleEditorViewModel form, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        if (!ModelState.IsValid)
        {
            var rules = await _ruleStore.GetAsync(workspace.TenantKey, cancellationToken);
            return View("Index", new WorkflowRulePageViewModel
            {
                WorkspaceCode = workspace.TenantKey,
                WorkspaceName = workspace.Name,
                Rules = rules,
                Editor = form
            });
        }

        if (form.Channel is not NotificationChannelDto.Sms and not NotificationChannelDto.Email)
        {
            ModelState.AddModelError(nameof(form.Channel), "Workflow hatirlatmalari su an yalnizca SMS veya e-posta olarak gonderilebilir.");
            var rules = await _ruleStore.GetAsync(workspace.TenantKey, cancellationToken);
            return View("Index", new WorkflowRulePageViewModel
            {
                WorkspaceCode = workspace.TenantKey,
                WorkspaceName = workspace.Name,
                Rules = rules,
                Editor = form
            });
        }

        await _ruleStore.AddAsync(workspace.TenantKey, new WorkflowRuleDefinition
        {
            Name = form.Name.Trim(),
            Status = form.Status,
            ThresholdHours = form.ThresholdHours,
            Channel = form.Channel,
            MessageTemplate = form.MessageTemplate.Trim(),
            OnlyOncePerShipment = form.OnlyOncePerShipment,
            IsEnabled = form.IsEnabled,
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken);

        TempData["StudioMessage"] = "Workflow kurali kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        await _ruleStore.RemoveAsync(workspace.TenantKey, id, cancellationToken);
        TempData["StudioMessage"] = "Workflow kurali silindi.";
        return RedirectToAction(nameof(Index));
    }
}
