using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// KVKK aydinlatma metni, kullanim sartlari, cerez politikasi gibi
/// herkese acik yasal sayfalar.
/// </summary>
[AllowAnonymous]
[Route("legal")]
public sealed class LegalController : Controller
{
    [HttpGet("privacy")]
    public IActionResult Privacy() => View();

    [HttpGet("terms")]
    public IActionResult Terms() => View();

    [HttpGet("cookies")]
    public IActionResult Cookies() => View();

    [HttpGet("data-deletion")]
    public IActionResult DataDeletion() => View();
}
