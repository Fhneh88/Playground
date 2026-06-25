using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public class CurrencyApiClient
{
    private readonly HttpClient _httpClient;

    public CurrencyApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal> GetRateAsync(string from, string to)
    {
        using var response = await _httpClient.GetAsync($"v6/latest/{from}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new CurrencyApiResponseJsonConverter());
        var data = JsonSerializer.Deserialize<CurrencyApiResponse>(content, options);

        if (data?.Rates != null && data.Rates.TryGetValue(to, out var rate))
        {
            return rate;
        }

        throw new InvalidOperationException($"Exchange rate from {from} to {to} not found.");
    }
}

public class CurrencyApiResponse
{
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("documentation")]
    public string? Documentation { get; set; }

    [JsonPropertyName("terms_of_use")]
    public string? TermsOfUse { get; set; }

    [JsonPropertyName("time_last_update_unix")]
    public int TimeLastUpdateUnix { get; set; }

    [JsonPropertyName("time_last_update_utc")]
    public string? TimeLastUpdateUtc { get; set; }

    [JsonPropertyName("time_next_update_unix")]
    public int TimeNextUpdateUnix { get; set; }

    [JsonPropertyName("time_next_update_utc")]
    public string? TimeNextUpdateUtc { get; set; }

    [JsonPropertyName("time_eol_unix")]
    public int TimeEolUnix { get; set; }

    [JsonPropertyName("base_code")]
    public string? BaseCode { get; set; }

    public Dictionary<string, decimal> Rates { get; set; } = new();
}