namespace OrderService.Core.Models;

public class Order
{
    public int           Id            { get; set; }
    public string        CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items       { get; set; } = new();
    public decimal       TotalAmount   { get; set; }
    public OrderStatus   Status        { get; set; }
    public DateTime      CreatedAt     { get; set; }
}
