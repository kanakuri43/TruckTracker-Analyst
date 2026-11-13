namespace TruckTrackerAnalyst.Domain;

/// <summary>
/// 得意先別 損益 = 粗利(販売管理) − 配送経費(配送管理)。TruckTracker admin.js の aggregateDestData と同じ按分。
/// 経費 = 支店の期間経費 ÷ その支店の回数合計 × 得意先の回数。
/// </summary>
public static class ProfitCalculator
{
    /// <param name="branchMonthlyExpense">支店ID → 月間経費</param>
    /// <param name="profitByCode">得意先コード → 粗利（期間内の販売管理）</param>
    public static ProfitReport Calculate(
        IEnumerable<Delivery> deliveries0,
        IReadOnlyDictionary<Guid, decimal> branchMonthlyExpense,
        decimal months,
        IReadOnlyDictionary<string, decimal> profitByCode,
        CountMode mode = CountMode.Count)
    {
        var profits = profitByCode.ToDictionary(kv => NormalizeCode(kv.Key)!, kv => kv.Value);
        var deliveries = deliveries0.Where(d => d.Stops.Count > 0).ToList();
        // コードを引けない記録（course_stop の紐付け切れ等）は、同名の配達先の他の記録から補う（TruckTracker 本体の集計と同じ）。
        var codeByName = deliveries.SelectMany(d => d.Stops)
            .Select(s => (s.DestinationName, Code: NormalizeCode(s.SalesCustomerCode))).Where(x => x.Code is not null)
            .GroupBy(x => x.DestinationName).ToDictionary(g => g.Key, g => g.First().Code);
        var branchCountSum = new Dictionary<Guid, decimal>();
        var customers = new Dictionary<string, Acc>();

        foreach (var d in deliveries)
        {
            var share = mode == CountMode.Net ? 1m / d.Stops.Count : 1m;
            foreach (var s in d.Stops)
            {
                var code = NormalizeCode(s.SalesCustomerCode) ?? codeByName.GetValueOrDefault(s.DestinationName);
                var key = code ?? "name:" + s.DestinationName;   // コードなしは配達先名単位
                if (!customers.TryGetValue(key, out var a)) customers[key] = a = new Acc(s.DestinationName, code);
                a.Count += share;
                if (d.BranchId is { } b)
                {
                    a.BranchCounts[b] = a.BranchCounts.GetValueOrDefault(b) + share;
                    branchCountSum[b] = branchCountSum.GetValueOrDefault(b) + share;
                }
            }
        }

        var perCount = branchCountSum.ToDictionary(
            kv => kv.Key,
            kv => kv.Value == 0 ? 0 : branchMonthlyExpense.GetValueOrDefault(kv.Key) * months / kv.Value);

        var rows = customers.Values
            .Select(a =>
            {
                var expense = a.BranchCounts.Sum(kv => kv.Value * perCount[kv.Key]);
                if (a.Code is null) return new CustomerProfit(a.Name, null, a.Count, expense, null, Warning.NoCode);
                return profits.TryGetValue(a.Code, out var p)
                    ? new CustomerProfit(a.Name, a.Code, a.Count, expense, p, Warning.None)
                    : new CustomerProfit(a.Name, a.Code, a.Count, expense, 0, Warning.NoSales);
            })
            .OrderBy(r => r.Name, StringComparer.Create(new System.Globalization.CultureInfo("ja-JP"), false))
            .ToList();
        return new ProfitReport(rows);
    }

    /// <summary>コード比較用の正規化。数値なら先頭ゼロ・空白を除去（Excel の VALUE() 相当）。空は null。</summary>
    public static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim();
        return long.TryParse(code, out var n) ? n.ToString() : code;
    }

    private sealed class Acc(string name, string? code)
    {
        public string Name { get; } = name;
        public string? Code { get; } = code;
        public decimal Count;
        public Dictionary<Guid, decimal> BranchCounts { get; } = new();
    }
}


