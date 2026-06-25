using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient<CurrencyApiClient>(c =>
{
    c.BaseAddress = new Uri("https://open.er-api.com/");
    c.Timeout = TimeSpan.FromSeconds(5);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy())
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5)));

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .Or<TimeoutRejectedException>()
        .WaitAndRetryAsync(
            3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                var reason = outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString();
                Console.WriteLine($"Попытка #{retryAttempt} после {timespan.TotalSeconds:N0}с по причине: {reason}");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));
}

var host = builder.Build();
var client = host.Services.GetRequiredService<CurrencyApiClient>();

try
{
    foreach (var to in new[] { "RUB", "EUR", "GBP" })
    {
        var rate = await client.GetRateAsync("USD", to);
        Console.WriteLine($"1 USD = {rate} {to}");
    }
}
catch (HttpRequestException)
{
    Console.WriteLine("Сеть недоступна или сервис валют не отвечает. Пожалуйста, проверьте подключение.");
}
catch (TimeoutException)
{
    Console.WriteLine("Сервис валют не ответил в течение ожидания. Пожалуйста, попробуйте позже.");
}
catch (Exception)
{
    Console.WriteLine("Произошла ошибка при получении курса валют. Пожалуйста, попробуйте позже.");
}
