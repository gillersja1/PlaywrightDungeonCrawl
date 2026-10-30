using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Pages.Certificates;

/// <summary>A certificate's public page. If it's here, the dungeon issued it.</summary>
public class ViewModel(IPlayerRecords records, DungeonCatalog catalog) : PageModel
{
    public Certificate Certificate { get; private set; } = null!;
    public IReadOnlyList<Floor> Floors { get; private set; } = [];
    public bool IsOwner { get; private set; }
    public string VerifyUrl { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (await records.CertificateAsync(id, HttpContext.RequestAborted) is not { } certificate)
        {
            return NotFound();
        }
        Certificate = certificate;
        Floors = catalog.Floors.Take(certificate.Floors).ToList();
        IsOwner = User.FindFirstValue(ClaimTypes.NameIdentifier) == certificate.PlayerId;
        VerifyUrl = $"{Request.Scheme}://{Request.Host}/certificates/{certificate.Id}";
        return Page();
    }
}
