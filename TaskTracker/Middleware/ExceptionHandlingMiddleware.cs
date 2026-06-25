using System.Diagnostics;
using System.Net.Mime;
using System.Text.Json;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            object body = env.IsProduction()
                ? (object)new { error = "Internal Server Error", traceId = Activity.Current?.Id }
                : new { error = "Internal Server Error", message = ex.Message, traceId = Activity.Current?.Id };

            await context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}
