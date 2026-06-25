using System;
using PaymentModule;

class Program
{
    static void Main()
    {
        PaymentMethod[] methods =
        {
            new CardPayment(),
            new WalletPayment(),
            new CryptoPayment(),
            new BankTransferPayment()
        };

        Console.WriteLine("Обработка платежа на 5000:");
        PaymentProcessor.ProcessAll(methods, 5000m);

        Console.WriteLine();
        Console.WriteLine("Проверка лимита кошелька на 15000:");

        PaymentMethod[] walletOnly =
        {
            new WalletPayment()
        };

        PaymentProcessor.ProcessAll(walletOnly, 15000m);
    }
}