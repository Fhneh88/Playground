namespace PaymentModule;

public class PaymentMethod
{
    public virtual string Process(decimal amount)
    {
        return $"PaymentMethod: обработано {amount:F2}";
    }
}

public class CardPayment : PaymentMethod
{
    public override string Process(decimal amount)
    {
        decimal total = amount * 1.02m;
        return $"CardPayment: списано {total:F2} (комиссия 2%)";
    }
}

public class WalletPayment : PaymentMethod
{
    public const decimal Limit = 10000m;

    public override string Process(decimal amount)
    {
        if (amount > Limit)
        {
            return $"WalletPayment: ошибка, превышен лимит {Limit:F2}";
        }

        return $"WalletPayment: списано {amount:F2}";
    }
}

public class CryptoPayment : PaymentMethod
{
    public const decimal BtcRate = 60000m;

    public override string Process(decimal amount)
    {
        decimal btcAmount = amount / BtcRate;
        return $"CryptoPayment: списано {btcAmount:F4} BTC (эквивалент {amount:F2})";
    }
}

public class BankTransferPayment : PaymentMethod
{
    public override string Process(decimal amount)
    {
        return $"BankTransferPayment: перевод выполнен на сумму {amount:F2}";
    }
}

public static class PaymentProcessor
{
    public static string[] ProcessAll(PaymentMethod[] payments, decimal amount)
    {
        return payments.Select(p => p.Process(amount)).ToArray();
    }
}