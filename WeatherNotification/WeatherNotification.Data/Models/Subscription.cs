namespace WeatherNotification.Data.Models;

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string City { get; set; }
    public required string Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
