using OrdersLinqDemo.Data;

// ─── Инициализация БД ───────────────────────────────────────────────────────
using var context = new AppDbContext();
context.Database.EnsureCreated();
DbSeeder.Seed(context);

// Загружаем все заказы из SQLite в память для LINQ-запросов
var orders = context.Orders.ToList();

PrintHeader("Все заказы в базе данных");
foreach (var o in orders)
    Console.WriteLine($"  #{o.Id} | {o.CustomerName,-10} | {o.City,-20} | {o.Total,7} руб. | {o.CreatedAt:dd.MM.yyyy} | {(o.IsPaid ? "✓ Оплачен" : "✗ Не оплачен")}");

// ─── Задача 1: Оплаченные заказы > 1000, от новых к старым ─────────────────
PrintHeader("Задача 1 — Оплаченные заказы свыше 1000 руб. (новые → старые)");

var paidExpensive = orders
    .Where(o => o.IsPaid && o.Total > 1000)
    .OrderByDescending(o => o.CreatedAt)
    .ToList();

foreach (var o in paidExpensive)
    Console.WriteLine($"  Заказ #{o.Id}: {o.CustomerName}, {o.Total} руб., {o.CreatedAt:dd.MM.yyyy}");

// ─── Задача 2: Группировка по городу ───────────────────────────────────────
PrintHeader("Задача 2 — Заказы по городам");

var byCity = orders
    .GroupBy(o => o.City)
    .Select(g => new
    {
        City     = g.Key,
        Count    = g.Count(),
        TotalSum = g.Sum(o => o.Total)
    })
    .OrderByDescending(x => x.TotalSum)
    .ToList();

foreach (var c in byCity)
    Console.WriteLine($"  {c.City,-20} | заказов: {c.Count} | сумма: {c.TotalSum} руб.");

// ─── Задача 3: Клиент с максимальной суммой ─────────────────────────────────
PrintHeader("Задача 3 — Клиент с наибольшей суммарной покупкой");

var topCustomer = orders
    .GroupBy(o => o.CustomerName)
    .Select(g => new
    {
        Name        = g.Key,
        TotalSpent  = g.Sum(o => o.Total),
        OrdersCount = g.Count()
    })
    .MaxBy(x => x.TotalSpent)!;

Console.WriteLine($"  Победитель: {topCustomer.Name}");
Console.WriteLine($"  Потрачено:  {topCustomer.TotalSpent} руб. ({topCustomer.OrdersCount} заказа/заказов)");

// ─── Задача 4: Города с неоплаченными заказами ──────────────────────────────
PrintHeader("Задача 4 — Города с неоплаченными заказами");

var citiesWithUnpaid = orders
    .Where(o => !o.IsPaid)
    .Select(o => o.City)
    .Distinct()
    .OrderBy(city => city)
    .ToList();

foreach (var city in citiesWithUnpaid)
    Console.WriteLine($"  • {city}");

Console.WriteLine("\nГотово!");

// ─── Вспомогательный метод ──────────────────────────────────────────────────
static void PrintHeader(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('═', 55));
    Console.WriteLine($"  {title}");
    Console.WriteLine(new string('═', 55));
}
