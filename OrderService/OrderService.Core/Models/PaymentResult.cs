namespace OrderService.Core.Models;

public class PaymentResult
{
    public bool    Success       { get; set; }
    public string? TransactionId { get; set; }
    public string? Error         { get; set; }

    public static PaymentResult Succeeded(string txId) =>
        new() { Success = true, TransactionId = txId };

    public static PaymentResult Failed(string error) =>
        new() { Success = false, Error = error };
}
