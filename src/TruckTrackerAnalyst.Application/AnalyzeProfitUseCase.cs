using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Application;

/// <summary>期間内の得意先別営業利益を算出する。販売管理・配送管理を並行して読み取り、Domain の計算に渡す。</summary>
public sealed class AnalyzeProfitUseCase(ISalesProfitRepository sales, IDeliveryRepository deliveries)
{
    public async Task<ProfitReport> ExecuteAsync(
        DateOnly from, DateOnly to, CountMode mode = CountMode.Count, CancellationToken ct = default)
    {
        var months = PeriodMonths.Between(from, to);   // from > to はここで例外
        var profitTask = sales.GetProfitByCodeAsync(from, to, ct);
        var deliveryTask = deliveries.GetAsync(from, to, ct);
        await Task.WhenAll(profitTask, deliveryTask);
        var d = deliveryTask.Result;
        return ProfitCalculator.Calculate(d.Deliveries, d.BranchMonthlyExpense, months, profitTask.Result, mode);
    }
}
