namespace TruckTrackerAnalyst.Domain;

/// <summary>
/// 得意先別 営業利益 = 粗利(販売管理) − 配送経費(配送管理)。TruckTracker admin.js の aggregateDestData と同じ按分。
/// 経費 = 支店の期間経費 ÷ その支店の回数合計 × 得意先の回数。
/// 距離加重（distanceShare &gt; 0）: 期間経費のうち distanceShare を「コースの走行距離の負担分（Stop.RouteKm）」で、残りを回数で按分する。
/// </summary>
public static class ProfitCalculator
{
    /// <param name="branchMonthlyExpense">支店ID → 月間経費</param>
    /// <param name="profitByCode">得意先コード → 粗利（期間内の販売管理）</param>
    /// <param name="distanceShare">期間経費のうち距離で按分する割合（0〜1）。0 = 回数のみ（従来の計算）。</param>
    public static ProfitReport Calculate(
        IEnumerable<Delivery> deliveries0,
        IReadOnlyDictionary<Guid, decimal> branchMonthlyExpense,
        decimal months,
        IReadOnlyDictionary<string, decimal> profitByCode,
        CountMode mode = CountMode.Count,
        decimal distanceShare = 0)
    {
        if (distanceShare is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(distanceShare));
        var profits = profitByCode.ToDictionary(kv => NormalizeCode(kv.Key)!, kv => kv.Value);
        var deliveries = deliveries0.Where(d => d.Stops.Count > 0).ToList();
        var stops = deliveries.SelectMany(d => d.Stops).ToList();
        // コードを引けない記録（course_stop の紐付け切れ等）は、同名の配達先の他の記録から補う（TruckTracker 本体の集計と同じ）。距離も同様。
        var codeByName = stops
            .Select(s => (s.DestinationName, Code: NormalizeCode(s.SalesCustomerCode))).Where(x => x.Code is not null)
            .GroupBy(x => x.DestinationName).ToDictionary(g => g.Key, g => g.First().Code);
        var kmByName = stops.Where(s => s.Km is not null)
            .GroupBy(s => s.DestinationName).ToDictionary(g => g.Key, g => g.First().Km!.Value);
        // それでも座標のない配達先は、座標のある到着記録の平均距離で扱う。
        var avgKm = stops.Where(s => s.Km is not null).Select(s => s.Km!.Value).DefaultIfEmpty(0).Average();
        // 距離按分に使う走行距離の負担分（RouteKm）も同様に補完。コースごとに値が違うので同名は平均。
        var routeByName = stops.Where(s => s.RouteKm is not null)
            .GroupBy(s => s.DestinationName).ToDictionary(g => g.Key, g => g.Average(s => s.RouteKm!.Value));
        var avgRoute = stops.Where(s => s.RouteKm is not null).Select(s => s.RouteKm!.Value).DefaultIfEmpty(0).Average();

        var branchCountSum = new Dictionary<Guid, decimal>();
        var branchKmSum = new Dictionary<Guid, decimal>();
        var customers = new Dictionary<string, Acc>();

        foreach (var d in deliveries)
        {
            var share = mode == CountMode.Net ? 1m / d.Stops.Count : 1m;
            foreach (var s in d.Stops)
            {
                var code = NormalizeCode(s.SalesCustomerCode) ?? codeByName.GetValueOrDefault(s.DestinationName);
                var key = code ?? "name:" + s.DestinationName;   // コードなしは配達先名単位
                if (!customers.TryGetValue(key, out var a)) customers[key] = a = new Acc(s.DestinationName, code);
                var km = s.Km ?? kmByName.GetValueOrDefault(s.DestinationName, avgKm);   // 表示用
                var route = s.RouteKm ?? routeByName.GetValueOrDefault(s.DestinationName, avgRoute);   // 按分用（コース総距離の負担分なので share は掛けない）
                a.Count += share;
                a.KmSum += share * km;
                if (d.BranchId is { } b)
                {
                    a.BranchCounts[b] = a.BranchCounts.GetValueOrDefault(b) + share;
                    a.BranchKm[b] = a.BranchKm.GetValueOrDefault(b) + route;
                    branchCountSum[b] = branchCountSum.GetValueOrDefault(b) + share;
                    branchKmSum[b] = branchKmSum.GetValueOrDefault(b) + route;
                }
            }
        }

        decimal Expense(Acc a, decimal share) => a.BranchCounts.Keys.Sum(b =>
        {
            var total = branchMonthlyExpense.GetValueOrDefault(b) * months;
            var kmSum = branchKmSum[b];
            var ds = kmSum == 0 ? 0 : share;   // 全員 0km なら距離で按分できない → 回数のみ
            var byCount = total / branchCountSum[b] * a.BranchCounts[b];   // 従来の計算（1配送あたり経費 × 回数）
            return ds == 0 ? byCount : (1 - ds) * byCount + ds * total * a.BranchKm[b] / kmSum;
        });

        var rows = customers.Values
            .Select(a =>
            {
                var expense = Expense(a, distanceShare);
                var km = a.Count == 0 ? (decimal?)null : a.KmSum / a.Count;
                if (a.Code is null) return new CustomerProfit(a.Name, null, a.Count, expense, null, Warning.NoCode, km);
                var hasSales = profits.TryGetValue(a.Code, out var p);
                // 回数のみ(0%)と距離のみ(100%)で黒字・赤字が逆転する = 按分の前提次第で結論が変わる得意先
                var flips = p - Expense(a, 0) < 0 != p - Expense(a, 1) < 0;
                return new CustomerProfit(a.Name, a.Code, a.Count, expense, p, hasSales ? Warning.None : Warning.NoSales, km, flips);
            })
            // 営業利益の昇順（赤字の大きい順）。営業利益なし（コード未設定）は末尾、同額は名前順
            .OrderBy(r => r.Net is null).ThenBy(r => r.Net)
            .ThenBy(r => r.Name, StringComparer.Create(new System.Globalization.CultureInfo("ja-JP"), false))
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
        public decimal KmSum;
        public Dictionary<Guid, decimal> BranchCounts { get; } = new();
        public Dictionary<Guid, decimal> BranchKm { get; } = new();
    }
}
