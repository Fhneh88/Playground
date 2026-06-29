using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using WeddingInvite.Models;
using WeddingInvite.Pages;
using WeddingInvite.Services;
using Xunit;

namespace WeddingInvite.Tests.Pages;

public class IndexModelTests
{
    private readonly Mock<IGoogleSheetsService> _sheetsMock = new();

    private IndexModel CreateModel() => new(_sheetsMock.Object);

    [Fact]
    public void OnGet_initialises_empty_Rsvp_property()
    {
        var model = CreateModel();

        model.OnGet();

        Assert.NotNull(model.Rsvp);
    }

    [Fact]
    public async Task OnPostAsync_valid_model_calls_service_and_redirects_to_ThankYou()
    {
        var model = CreateModel();
        model.Rsvp = new RsvpModel
        {
            GuestName = "Иван Петров",
            Starter = "Карпаччо из говядины",
            MainCourse = "Говядина Веллингтон",
            Dessert = "Шоколадный фондан"
        };

        var result = await model.OnPostAsync();

        _sheetsMock.Verify(s => s.AppendRowAsync(model.Rsvp), Times.Once);
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("ThankYou", redirect.PageName);
        Assert.Equal("Иван Петров", redirect.RouteValues!["name"]);
    }

    [Fact]
    public async Task OnPostAsync_invalid_model_returns_page_without_calling_service()
    {
        var model = CreateModel();
        model.ModelState.AddModelError("Rsvp.GuestName", "Required");

        var result = await model.OnPostAsync();

        _sheetsMock.Verify(s => s.AppendRowAsync(It.IsAny<RsvpModel>()), Times.Never);
        Assert.IsType<PageResult>(result);
    }
}
