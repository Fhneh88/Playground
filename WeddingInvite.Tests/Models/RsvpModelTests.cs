using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WeddingInvite.Models;
using Xunit;

namespace WeddingInvite.Tests.Models;

public class RsvpModelTests
{
    private static List<ValidationResult> Validate(RsvpModel model)
    {
        var ctx = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_model_passes_validation()
    {
        var model = new RsvpModel
        {
            GuestName = "Иван Петров",
            Starter = "Карпаччо из говядины",
            MainCourse = "Говядина Веллингтон",
            Dessert = "Шоколадный фондан",
            Drinks = new List<string> { "Вино красное" }
        };

        var errors = Validate(model);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("", "Карпаччо", "Говядина", "Торт")]
    [InlineData("Иван", "", "Говядина", "Торт")]
    [InlineData("Иван", "Карпаччо", "", "Торт")]
    [InlineData("Иван", "Карпаччо", "Говядина", "")]
    public void Missing_required_field_fails_validation(
        string name, string starter, string main, string dessert)
    {
        var model = new RsvpModel
        {
            GuestName = name,
            Starter = starter,
            MainCourse = main,
            Dessert = dessert
        };

        var errors = Validate(model);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Drinks_list_can_be_empty_and_model_is_still_valid()
    {
        var model = new RsvpModel
        {
            GuestName = "Анна Смирнова",
            Starter = "Тартар из лосося",
            MainCourse = "Семга на гриле",
            Dessert = "Панна котта",
            Drinks = new List<string>()
        };

        var errors = Validate(model);

        Assert.Empty(errors);
    }
}
