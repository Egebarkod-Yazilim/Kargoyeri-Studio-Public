using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P3-#5 — Tenant onboarding wizard.
/// Yeni musterilere kurulum akisini gorsel adimlarla anlatir:
///   1) Workspace - 2) API Key - 3) Test Gonderi - 4) Webhook
/// Her adim, ilgili sayfaya yonlendirir.
/// </summary>
[Authorize]
[NonController]
[Route("onboarding")]
public class OnboardingController : Controller
{
    [HttpGet("")]
    [HttpGet("step/{step:int?}")]
    public IActionResult Index(int step = 1)
    {
        if (step < 1) step = 1;
        if (step > 4) step = 4;
        ViewData["Title"] = "Hoş geldin";
        ViewData["Step"] = step;
        return View("Index");
    }
}
