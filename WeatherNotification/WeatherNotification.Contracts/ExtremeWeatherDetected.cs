namespace WeatherNotification.Contracts;

public record ExtremeWeatherDetected(Guid SubscriptionId, string City, double TemperatureC);
