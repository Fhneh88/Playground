using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WeddingInvite.Models;

public class RsvpModel
{
    [Required(ErrorMessage = "Пожалуйста, введите ваше имя")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 100 символов")]
    public string GuestName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите холодную закуску")]
    public string Starter { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите горячее блюдо")]
    public string MainCourse { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите десерт")]
    public string Dessert { get; set; } = string.Empty;

    public List<string> Drinks { get; set; } = new();
}
