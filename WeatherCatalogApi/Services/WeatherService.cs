using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeatherCatalogApi.Services;

public record WeatherResult(string City, int TemperatureC, string Description, int Humidity);

public class WeatherService(HttpClient http)
{
    public async Task<WeatherResult?> GetWeatherAsync(string city)
    {
        try
        {
            var response = await http.GetAsync($"https://wttr.in/{Uri.EscapeDataString(city)}?format=j1");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonSerializer.Deserialize<WttrRoot>(json);

            if (root?.CurrentCondition is not [var current, ..])
                return null;

            var areaName = root.NearestArea?.FirstOrDefault()?.AreaName?.FirstOrDefault()?.Value ?? city;
            var description = current.WeatherDesc?.FirstOrDefault()?.Value ?? "Unknown";

            return new WeatherResult(
                City: areaName,
                TemperatureC: int.Parse(current.TempC),
                Description: description,
                Humidity: int.Parse(current.Humidity)
            );
        }
        catch
        {
            return null;
        }
    }

    // wttr.in JSON deserialization DTOs
    private sealed class WttrRoot
    {
        [JsonPropertyName("current_condition")]
        public List<CurrentCondition>? CurrentCondition { get; set; }

        [JsonPropertyName("nearest_area")]
        public List<NearestArea>? NearestArea { get; set; }
    }

    private sealed class CurrentCondition
    {
        [JsonPropertyName("temp_C")]
        public string TempC { get; set; } = "0";

        [JsonPropertyName("humidity")]
        public string Humidity { get; set; } = "0";

        [JsonPropertyName("weatherDesc")]
        public List<ValueWrapper>? WeatherDesc { get; set; }
    }

    private sealed class NearestArea
    {
        [JsonPropertyName("areaName")]
        public List<ValueWrapper>? AreaName { get; set; }
    }

    private sealed class ValueWrapper
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }
}
