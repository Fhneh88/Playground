using System.Diagnostics;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;
        var startedAt = DateTime.UtcNow;

        logger.LogInformation("→ {Method} {Path} started at {StartedAt:HH:mm:ss.fff}",
            method, path, startedAt);

        var sw = Stopwatch.StartNew();

        await next(context);

        sw.Stop();

        logger.LogInformation("← {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            method, path, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
}
