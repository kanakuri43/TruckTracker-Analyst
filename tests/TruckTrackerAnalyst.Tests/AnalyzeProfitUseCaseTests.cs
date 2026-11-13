using TruckTrackerAnalyst.Application;
using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Tests;

public class AnalyzeProfitUseCaseTests
{
    sealed class FakeSales(Dictionary<string, decimal> p) : ISalesProfitRepository
    {
        public DateOnly? From, To;
        public Task<IReadOnlyDictionary<string, decimal>> GetProfitByCodeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        { From = from; To = to; return Task.FromResult<IReadOnlyDictionary<string, decimal>>(p); }
    }

    sealed class FakeDeliveries(DeliveryData d) : IDeliveryRepository
    {
        public Task<DeliveryData> GetAsync(DateOnly from, DateOnly to, CancellationToken ct = default) => Task.FromResult(d);
    }

    [Fact]
    public async Task CombinesSalesAndDeliveries_OverPeriod()
    {
        var b = Guid.NewGuid();
        var sales = new FakeSales(new() { ["100"] = 10_000 });
        var uc = new AnalyzeProfitUseCase(sales, new FakeDeliveries(new DeliveryData(
            [new Delivery(b, [new Stop("A", "100")]), new Delivery(b, [new Stop("A", "100")])],
            new Dictionary<Guid, decimal> { [b] = 1_000 })));

        var r = await uc.ExecuteAsync(new(2026, 6, 1), new(2026, 8, 31));

        Assert.Equal(new DateOnly(2026, 6, 1), sales.From);
        Assert.Equal(new DateOnly(2026, 8, 31), sales.To);
        var row = Assert.Single(r.Rows);
        Assert.Equal(2, row.Count);
        Assert.Equal(3_000, row.Expense);     // 月間経費1,000 × 3か月
        Assert.Equal(7_000, row.Net);
    }

    [Fact]
    public async Task InvertedPeriod_Throws() =>
        await Assert.ThrowsAsync<ArgumentException>(() => new AnalyzeProfitUseCase(
            new FakeSales([]), new FakeDeliveries(new DeliveryData([], new Dictionary<Guid, decimal>())))
            .ExecuteAsync(new(2026, 8, 1), new(2026, 6, 1)));
}
