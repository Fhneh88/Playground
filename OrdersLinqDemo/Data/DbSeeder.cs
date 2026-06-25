using OrdersLinqDemo.Models;

namespace OrdersLinqDemo.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Orders.Any())
        {
            Console.WriteLine("База данных уже содержит данные. Пропуск заполнения.\n");
            return;
        }

        var orders = new List<Order>
        {
            new Order { Id = 1, CustomerName = "Иван",    City = "Москва",            Total = 1500, CreatedAt = new DateTime(2025, 1, 10), IsPaid = true  },
            new Order { Id = 2, CustomerName = "Мария",   City = "Санкт-Петербург",   Total = 3200, CreatedAt = new DateTime(2025, 1, 15), IsPaid = false },
            new Order { Id = 3, CustomerName = "Иван",    City = "Москва",            Total = 800,  CreatedAt = new DateTime(2025, 2,  3), IsPaid = true  },
            new Order { Id = 4, CustomerName = "Алексей", City = "Казань",            Total = 5400, CreatedAt = new DateTime(2025, 2, 20), IsPaid = true  },
            new Order { Id = 5, CustomerName = "Мария",   City = "Санкт-Петербург",   Total = 2100, CreatedAt = new DateTime(2025, 3,  1), IsPaid = true  },
            new Order { Id = 6, CustomerName = "Иван",    City = "Москва",            Total = 4700, CreatedAt = new DateTime(2025, 3, 12), IsPaid = false },
            new Order { Id = 7, CustomerName = "Елена",   City = "Казань",            Total = 900,  CreatedAt = new DateTime(2025, 3, 25), IsPaid = true  },
        };

        context.Orders.AddRange(orders);
        context.SaveChanges();
        Console.WriteLine($"✓ Добавлено {orders.Count} заказов в базу данных.\n");
    }
}
