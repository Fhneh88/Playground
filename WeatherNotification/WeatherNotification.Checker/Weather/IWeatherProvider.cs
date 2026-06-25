namespace WeatherNotification.Checker.Weather;

public interface IWeatherProvider
{
    Task<double> GetTemperatureAsync(string city, CancellationToken cancellationToken = default);
}
