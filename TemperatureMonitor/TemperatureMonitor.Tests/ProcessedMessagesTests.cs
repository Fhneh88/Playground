using Consumer;

namespace TemperatureMonitor.Tests;

/// <summary>
/// Тесты идемпотентного хранилища обработанных сообщений.
/// </summary>
public class ProcessedMessagesTests
{
    private readonly ProcessedMessages _sut = new();

    // --- TryAdd: базовые сценарии ---

    [Fact]
    public void TryAdd_NewId_ReturnsTrue()
    {
        var result = _sut.TryAdd(Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public void TryAdd_SameIdSecondTime_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        _sut.TryAdd(id);

        var result = _sut.TryAdd(id);

        Assert.False(result);
    }

    // --- Граничные случаи ---

    [Fact]
    public void TryAdd_MultipleUniqueIds_AllReturnTrue()
    {
        var results = Enumerable.Range(0, 10)
            .Select(_ => _sut.TryAdd(Guid.NewGuid()))
            .ToList();

        Assert.All(results, r => Assert.True(r));
    }

    [Fact]
    public void TryAdd_AfterDuplicate_NewIdIsStillAccepted()
    {
        var duplicate = Guid.NewGuid();
        _sut.TryAdd(duplicate);
        _sut.TryAdd(duplicate);   // дубль

        var result = _sut.TryAdd(Guid.NewGuid());

        Assert.True(result);
    }
}
