namespace Contracts;

public record TemperatureReading(
    Guid MessageId,
    string City,
    double TemperatureC,
    DateTime MeasuredAt);
