namespace WeatherNotification.Checker.Weather;

public class WeatherProviderException : Exception
{
    public WeatherProviderException(string message) : base(message) { }
    public WeatherProviderException(string message, Exception inner) : base(message, inner) { }
}
