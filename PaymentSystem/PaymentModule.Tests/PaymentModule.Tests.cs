using PaymentModule;
using Xunit;

namespace PaymentModule.Tests;

public class PaymentTests
{
    [Fact]
    public void CardPayment_Adds_2Percent_Fee()
    {
        PaymentMethod payment = new CardPayment();

        string result = payment.Process(1000m);

        Assert.Equal("CardPayment: списано 1020.00 (комиссия 2%)", result);
    }

    [Theory]
    [InlineData(100,     "CardPayment: списано 102.00 (комиссия 2%)")]
    [InlineData(0,       "CardPayment: списано 0.00 (комиссия 2%)")]
    [InlineData(9999.99, "CardPayment: списано 10199.99 (комиссия 2%)")]
    public void CardPayment_Adds_2Percent_Fee_ForVariousAmounts(decimal amount, string expected)
    {
        PaymentMethod payment = new CardPayment();

        string result = payment.Process(amount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void WalletPayment_Allows_Amount_Within_Limit()
    {
        PaymentMethod payment = new WalletPayment();

        string result = payment.Process(5000m);

        Assert.Equal("WalletPayment: списано 5000.00", result);
    }

    [Fact]
    public void WalletPayment_Rejects_Amount_Above_Limit()
    {
        PaymentMethod payment = new WalletPayment();

        string result = payment.Process(15000m);

        Assert.Equal("WalletPayment: ошибка, превышен лимит 10000.00", result);
    }

    [Theory]
    [InlineData(0,        "WalletPayment: списано 0.00")]
    [InlineData(9999.99,  "WalletPayment: списано 9999.99")]
    [InlineData(10000,    "WalletPayment: списано 10000.00")]   // exactly at limit passes (strict >)
    [InlineData(10000.01, "WalletPayment: ошибка, превышен лимит 10000.00")]
    public void WalletPayment_BoundaryBehavior(decimal amount, string expected)
    {
        PaymentMethod payment = new WalletPayment();

        string result = payment.Process(amount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CryptoPayment_Converts_To_Btc()
    {
        PaymentMethod payment = new CryptoPayment();

        string result = payment.Process(5000m);

        Assert.Equal("CryptoPayment: списано 0.0833 BTC (эквивалент 5000.00)", result);
    }

    [Theory]
    [InlineData(0,     "CryptoPayment: списано 0.0000 BTC (эквивалент 0.00)")]
    [InlineData(60000, "CryptoPayment: списано 1.0000 BTC (эквивалент 60000.00)")]
    public void CryptoPayment_Converts_To_Btc_ForVariousAmounts(decimal amount, string expected)
    {
        PaymentMethod payment = new CryptoPayment();

        string result = payment.Process(amount);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BankTransferPayment_Processes_Without_Extra_Fees()
    {
        PaymentMethod payment = new BankTransferPayment();

        string result = payment.Process(5000m);

        Assert.Equal("BankTransferPayment: перевод выполнен на сумму 5000.00", result);
    }

    [Fact]
    public void BankTransferPayment_Processes_ZeroAmount()
    {
        PaymentMethod payment = new BankTransferPayment();

        string result = payment.Process(0m);

        Assert.Equal("BankTransferPayment: перевод выполнен на сумму 0.00", result);
    }

    [Fact]
    public void BasePaymentMethod_Process_ReturnsBaseMessage()
    {
        PaymentMethod payment = new PaymentMethod();

        string result = payment.Process(1000m);

        Assert.Equal("PaymentMethod: обработано 1000.00", result);
    }

    [Fact]
    public void ProcessAll_Uses_Correct_Override_For_Each_Type()
    {
        PaymentMethod[] payments =
        {
            new CardPayment(),
            new WalletPayment(),
            new CryptoPayment(),
            new BankTransferPayment()
        };

        string[] results = PaymentProcessor.ProcessAll(payments, 5000m);

        Assert.Equal(4, results.Length);
        Assert.Equal("CardPayment: списано 5100.00 (комиссия 2%)", results[0]);
        Assert.Equal("WalletPayment: списано 5000.00", results[1]);
        Assert.Equal("CryptoPayment: списано 0.0833 BTC (эквивалент 5000.00)", results[2]);
        Assert.Equal("BankTransferPayment: перевод выполнен на сумму 5000.00", results[3]);
    }

    [Fact]
    public void ProcessAll_EmptyArray_ReturnsEmptyResults()
    {
        string[] results = PaymentProcessor.ProcessAll([], 1000m);

        Assert.Empty(results);
    }

    [Fact]
    public void ProcessAll_SingleElement_ReturnsOneResult()
    {
        PaymentMethod[] payments = [new CardPayment()];

        string[] results = PaymentProcessor.ProcessAll(payments, 1000m);

        Assert.Single(results);
        Assert.Equal("CardPayment: списано 1020.00 (комиссия 2%)", results[0]);
    }
}