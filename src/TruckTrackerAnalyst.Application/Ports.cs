using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Application;

/// <summary>販売管理（読み取り専用）。</summary>
public interface ISalesProfitRepository
{
    /// <summary>期間内（伝票日付）の得意先コード → 粗利。</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetProfitByCodeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

/// <summary>配送管理 = TruckTracker（読み取り専用）。</summary>
public interface IDeliveryRepository
{
    Task<DeliveryData> GetAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

/// <param name="Deliveries">期間内の配送（到着記録のある配達先のみ）</param>
/// <param name="BranchMonthlyExpense">支店ID → 月間経費</param>
public sealed record DeliveryData(
    IReadOnlyList<Delivery> Deliveries,
    IReadOnlyDictionary<Guid, decimal> BranchMonthlyExpense);
