using System.Net.Http.Json;
using WeatherNotification.Checker.Weather.Models;

namespace WeatherNotification.Checker.Weather;

public class OpenMeteoClient : IWeatherProvider
{
    private readonly HttpClient _forecastClient;
    private readonly HttpClient _geocodingClient;
    private readonly ILogger<OpenMeteoClient> _logger;

    public OpenMeteoClient(IHttpClientFactory factory, ILogger<OpenMeteoClient> logger)
    {
        _forecastClient = factory.CreateClient("OpenMeteoForecast");
        _geocodingClient = factory.CreateClient("OpenMeteoGeocoding");
        _logger = logger;
    }

    public async Task<double> GetTemperatureAsync(string city, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resolving coordinates for city {City}", city);

        GeocodingResponse? geo;
        try
        {
            geo = await _geocodingClient.GetFromJsonAsync<GeocodingResponse>(
                $"v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json",
                cancellationToken);
        }
        catch (Exception ex)
        {
            throw new WeatherProviderException($"Geocoding failed for city '{city}'", ex);
        }

        if (geo?.Results is not { Count: > 0 })
            throw new WeatherProviderException($"City '{city}' not found in geocoding API");

        var loc = geo.Results[0];
        _logger.LogInformation("City {City} → lat={Lat}, lon={Lon}", city, loc.Latitude, loc.Longitude);

        ForecastResponse? forecast;
        try
        {
            forecast = await _forecastClient.GetFromJsonAsync<ForecastResponse>(
                $"v1/forecast?latitude={loc.Latitude}&longitude={loc.Longitude}&current_weather=true",
                cancellationToken);
        }
        catch (Exception ex)
        {
            throw new WeatherProviderException($"Forecast fetch failed for city '{city}'", ex);
        }

        if (forecast?.CurrentWeather is null)
            throw new WeatherProviderException($"No weather data returned for city '{city}'");

        _logger.LogInformation("Temperature in {City}: {Temp}°C", city, forecast.CurrentWeather.Temperature);
        return forecast.CurrentWeather.Temperature;
    }
}
