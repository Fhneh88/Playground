using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WeddingInvite.Pages;

public class ThankYouModel : PageModel
{
    public string GuestName { get; private set; } = string.Empty;

    public void OnGet(string name)
    {
        GuestName = string.IsNullOrWhiteSpace(name) ? "Дорогой гость" : name;
    }
}
