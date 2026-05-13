using Microsoft.Extensions.DependencyInjection;
using Notifications.Filters;
using Notifications.Interfaces;
using Notifications.Senders;
using Notifications.Services;

// ── Service registration ────────────────────────────────────────────────────

var services = new ServiceCollection();

// Three senders registered against the same interface.
// The DI container collects them all into IEnumerable<INotificationSender>
// automatically when NotificationService asks for it.
services.AddTransient<INotificationSender, EmailSender>();
services.AddTransient<INotificationSender, SmsSender>();
services.AddTransient<INotificationSender, PushSender>();

// Singleton: one shared instance for the lifetime of the app.
// Safe because ProfanityFilter holds only a read-only set — no mutable state.
services.AddSingleton<INotificationFilter, ProfanityFilter>();

// Scoped: one instance per logical scope (e.g. per web request in a real app).
services.AddScoped<NotificationService>();

// ── Build container and run ─────────────────────────────────────────────────

var provider = services.BuildServiceProvider();

using var scope = provider.CreateScope();
var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

Console.WriteLine("=== Отправка обычного сообщения ===");
await notificationService.NotifyAllAsync("user@example.com", "Ваш заказ подтвержден");

Console.WriteLine();
Console.WriteLine("=== Попытка отправить сообщение с запрещённым словом ===");
await notificationService.NotifyAllAsync("user@example.com", "Это spam сообщение");
