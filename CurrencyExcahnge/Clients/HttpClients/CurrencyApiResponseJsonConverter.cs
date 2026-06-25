using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class CurrencyApiResponseJsonConverter : JsonConverter<CurrencyApiResponse>
{
    public override CurrencyApiResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var res = new CurrencyApiResponse();

        if (root.TryGetProperty("result", out var p)) res.Result = p.GetString();
        if (root.TryGetProperty("provider", out p)) res.Provider = p.GetString();
        if (root.TryGetProperty("documentation", out p)) res.Documentation = p.GetString();
        if (root.TryGetProperty("terms_of_use", out p)) res.TermsOfUse = p.GetString();
        if (root.TryGetProperty("time_last_update_unix", out p) && p.TryGetInt32(out var i1)) res.TimeLastUpdateUnix = i1;
        if (root.TryGetProperty("time_last_update_utc", out p)) res.TimeLastUpdateUtc = p.GetString();
        if (root.TryGetProperty("time_next_update_unix", out p) && p.TryGetInt32(out var i2)) res.TimeNextUpdateUnix = i2;
        if (root.TryGetProperty("time_next_update_utc", out p)) res.TimeNextUpdateUtc = p.GetString();
        if (root.TryGetProperty("time_eol_unix", out p) && p.TryGetInt32(out var i3)) res.TimeEolUnix = i3;
        if (root.TryGetProperty("base_code", out p)) res.BaseCode = p.GetString();

        if (root.TryGetProperty("rates", out var ratesElement) && ratesElement.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in ratesElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var d))
                {
                    dict[prop.Name] = d;
                }
                else if (prop.Value.ValueKind == JsonValueKind.String && Decimal.TryParse(prop.Value.GetString(), out var parsed))
                {
                    dict[prop.Name] = parsed;
                }
            }
            res.Rates = dict;
        }

        return res;
    }

    public override void Write(Utf8JsonWriter writer, CurrencyApiResponse value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Writing CurrencyApiResponse is not implemented.");
    }
}
