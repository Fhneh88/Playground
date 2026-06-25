using Microsoft.EntityFrameworkCore;
using WeatherNotification.Data;
using WeatherNotification.Data.Models;

namespace WeatherNotification.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/subscribe", async (SubscribeRequest request, SubscriptionsDbContext db,
            ILogger<SubscribeRequest> logger) =>
        {
            var subscription = new Subscription { City = request.City, Email = request.Email };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();

            logger.LogInformation("New subscription created: Id={Id}, City={City}, Email={Email}",
                subscription.Id, subscription.City, subscription.Email);

            return Results.Created($"/subscribe/{subscription.Id}", new { subscription.Id, subscription.City, subscription.Email });
        });

        app.MapDelete("/subscribe/{id:guid}", async (Guid id, SubscriptionsDbContext db,
            ILogger<SubscribeRequest> logger) =>
        {
            var subscription = await db.Subscriptions.FindAsync(id);
            if (subscription is null)
            {
                logger.LogWarning("Subscription {Id} not found for deletion", id);
                return Results.NotFound();
            }

            db.Subscriptions.Remove(subscription);
            await db.SaveChangesAsync();

            logger.LogInformation("Subscription deleted: Id={Id}, City={City}", id, subscription.City);
            return Results.NoContent();
        });
    }
}

public record SubscribeRequest(string City, string Email);
