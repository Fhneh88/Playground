# Plan: Logging System for PaymentModule

## Context
The payment module currently has no logging - results are only returned as strings and printed
via `Console.WriteLine` in `Program.cs`. The goal is to add a proper, injectable logging system
with levels (Info, Warning, Error) so that each payment operation records what happened and at
what severity. The system must be testable (loggers can be captured in tests) and must not break
any of the 19 existing passing tests.

---

## Approach

A custom `IPaymentLogger` interface lives inside `PaymentModule` - no NuGet packages needed.
Logger injection uses **optional constructor parameters** (defaulting to a `NullPaymentLogger`
singleton), so all existing `new CardPayment()` and `PaymentProcessor.ProcessAll(payments, amount)`
call sites continue to compile and pass unchanged.

---

## Files to Create / Modify

### 1. NEW - `PaymentModule/IPaymentLogger.cs`
Define the interface and log-level enum:
```csharp
namespace PaymentModule;

public enum LogLevel { Info, Warning, Error }

public interface IPaymentLogger
{
    void Log(LogLevel level, string message);
}
```

### 2. NEW - `PaymentModule/NullPaymentLogger.cs`
No-op singleton - used as the default when no logger is supplied:
```csharp
namespace PaymentModule;

public sealed class NullPaymentLogger : IPaymentLogger
{
    public static readonly NullPaymentLogger Instance = new();
    private NullPaymentLogger() { }
    public void Log(LogLevel level, string message) { }
}
```

### 3. NEW - `PaymentModule/ConsolePaymentLogger.cs`
Console sink used by `Program.cs`:
```csharp
namespace PaymentModule;

public sealed class ConsolePaymentLogger : IPaymentLogger
{
    public void Log(LogLevel level, string message)
    {
        string prefix = level switch
        {
            LogLevel.Warning => "[WARN] ",
            LogLevel.Error   => "[ERROR]",
            _                => "[INFO] "
        };
        Console.WriteLine($"{prefix} {message}");
    }
}
```

### 4. MODIFY - `PaymentModule/PaymentMethod.cs`
- Add `protected readonly IPaymentLogger _logger` to the base class.
- Add `optional IPaymentLogger? logger = null` constructor parameter to every class (base + 4 subclasses).
- Each `Process()` override calls `_logger.Log(...)` after computing the result string:
  - **CardPayment** → `LogLevel.Info`
  - **WalletPayment success** → `LogLevel.Info`
  - **WalletPayment limit exceeded** → `LogLevel.Warning`
  - **CryptoPayment** → `LogLevel.Info`
  - **BankTransferPayment** → `LogLevel.Info`
- `PaymentProcessor.ProcessAll` gains an optional third parameter `IPaymentLogger? logger = null`:
  - Logs `"Batch start: N payment(s), amount=X.XX"` (Info) before processing.
  - Logs `"Batch end: N processed, total=X.XX"` (Info) after processing.
- All return strings remain **byte-for-byte identical** - no existing `Assert.Equal` breaks.

### 5. MODIFY - `Program.cs`
Wire `ConsolePaymentLogger` into payment instances and `ProcessAll`:
```csharp
var logger = new ConsolePaymentLogger();
PaymentMethod[] methods =
{
    new CardPayment(logger),
    new WalletPayment(logger),
    new CryptoPayment(logger),
    new BankTransferPayment(logger)
};
PaymentProcessor.ProcessAll(methods, 5000m, logger);
```

### 6. NEW - `PaymentModule.Tests/CapturingPaymentLogger.cs`
Test double that records every log call:
```csharp
namespace PaymentModule.Tests;

public sealed class CapturingPaymentLogger : IPaymentLogger
{
    private readonly List<(LogLevel Level, string Message)> _entries = new();
    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;
    public void Log(LogLevel level, string message) => _entries.Add((level, message));
    public bool HasEntry(LogLevel level, string message)
        => _entries.Any(e => e.Level == level && e.Message == message);
    public int CountByLevel(LogLevel level)
        => _entries.Count(e => e.Level == level);
}
```

### 7. NEW - `PaymentModule.Tests/PaymentLoggingTests.cs`
New test class with facts covering:
| Test | What it asserts |
|---|---|
| `CardPayment_Logs_Info_On_Success` | Info entry with full result string |
| `CardPayment_LogsExactlyOneEntry_PerProcess` | Exactly 1 log entry per call |
| `WalletPayment_Logs_Info_When_Within_Limit` | Info on success |
| `WalletPayment_Logs_Warning_When_Limit_Exceeded` | Warning on limit breach |
| `WalletPayment_DoesNot_Log_Info_When_Limit_Exceeded` | No Info logged on failure |
| `CryptoPayment_Logs_Info_On_Conversion` | Info with BTC string |
| `BankTransferPayment_Logs_Info` | Info on transfer |
| `ProcessAll_Logs_BatchStart_With_Count_And_Amount` | Batch-start message content |
| `ProcessAll_Logs_BatchEnd_With_Count_And_Total` | Batch-end message content |
| `ProcessAll_BatchStart_Logged_Before_BatchEnd` | Ordering of log entries |
| `ProcessAll_EmptyBatch_Logs_ZeroItems` | Zero-count batch messages |
| `NoArgConstructor_UsesNullLogger_NoException` | Zero-arg constructors still work |
| `ProcessAll_NoLogger_DoesNotThrow` | Two-arg ProcessAll still works |

---

## Implementation Order
1. Create `IPaymentLogger.cs` - no dependents yet, safe
2. Create `NullPaymentLogger.cs` - depends only on step 1
3. Create `ConsolePaymentLogger.cs` - depends only on step 1
4. Modify `PaymentMethod.cs` - uses steps 1–2; existing tests still pass (optional params)
5. Modify `Program.cs` - wires step 3
6. Create `CapturingPaymentLogger.cs` in test project - depends on step 1
7. Create `PaymentLoggingTests.cs` in test project - depends on all prior steps

No `.csproj` files need to change. No NuGet packages are added.

---

## Verification
```bash
dotnet test PaymentModule.Tests/PaymentModule.Tests.csproj --verbosity minimal
```
- All 19 existing tests must still pass.
- ~13 new logging tests must pass.
- Total: ~32 passing tests.