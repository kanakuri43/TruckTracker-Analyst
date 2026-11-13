namespace TruckTrackerAnalyst.Domain;

/// <summary>配達先1件（到着記録あり）。</summary>
public sealed record Stop(string DestinationName, string? SalesCustomerCode);

/// <summary>1回の配送（report）。支店と、到着済みの配達先一覧。</summary>
public sealed record Delivery(Guid? BranchId, IReadOnlyList<Stop> Stops);

public enum CountMode { Count, Net }

public enum Warning { None, NoCode, NoSales }

/// <summary>得意先別の収益。Profit が null = 粗利算出不可（コード未設定）。</summary>
public sealed record CustomerProfit(
    string Name, string? Code, decimal Count, decimal Expense, decimal? Profit, Warning Warning)
{
    public decimal? Net => Profit - Expense;
}

public sealed record ProfitReport(IReadOnlyList<CustomerProfit> Rows)
{
    public decimal Count => Rows.Sum(r => r.Count);
    public decimal Expense => Rows.Sum(r => r.Expense);
    public decimal Profit => Rows.Sum(r => r.Profit ?? 0);
    public decimal Net => Profit - Expense;
}
