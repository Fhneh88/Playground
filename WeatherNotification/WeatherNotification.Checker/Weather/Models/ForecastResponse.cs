using System.Text.Json.Serialization;

namespace WeatherNotification.Checker.Weather.Models;

public class ForecastResponse
{
    [JsonPropertyName("current_weather")]
    public CurrentWeather? CurrentWeather { get; set; }
}

public class CurrentWeather
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}
