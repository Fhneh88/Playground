using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WeddingInvite.Models;
using WeddingInvite.Services;

namespace WeddingInvite.Pages;

public class IndexModel : PageModel
{
    private readonly IGoogleSheetsService _sheetsService;

    public IndexModel(IGoogleSheetsService sheetsService)
    {
        _sheetsService = sheetsService;
    }

    [BindProperty]
    public RsvpModel Rsvp { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        await _sheetsService.AppendRowAsync(Rsvp);
        return RedirectToPage("ThankYou", new { name = Rsvp.GuestName });
    }
}
