using TruckTrackerAnalyst.Domain;
using TruckTrackerAnalyst.Infrastructure;

namespace TruckTrackerAnalyst.Tests;

public partial class LiveReadOnlyTests
{
    // Excel「6~8月累計」シート: コード → (回数, 粗利)。粗利は VLOOKUP 近似一致の誤りを含み得る。
    static readonly Dictionary<string, (int Count, decimal Profit)> Quarter = new()
    {
        ["573524"] = (3, 49319), ["652974"] = (3, 5704), ["235351"] = (38, 84792), ["622792"] = (20, 449208),
        ["754005"] = (30, 1271781), ["522622"] = (2, 154887), ["235551"] = (35, 478352), ["225941"] = (1, 584259),
        ["862665"] = (11, 161355), ["227456"] = (21, 1358524), ["732647"] = (18, 733838), ["628002"] = (13, 38406),
        ["643784"] = (64, 581355), ["620433"] = (31, 1014037), ["635161"] = (4, 35047), ["623571"] = (1, 2000),
        ["235515"] = (4, 16190), ["524351"] = (9, 71958), ["621231"] = (2, 37545), ["633507"] = (27, 192194),
        ["221436"] = (2, 900), ["631450"] = (26, 322970), ["631818"] = (1, 1785), ["236648"] = (37, 91792),
        ["8622535"] = (2, 3462), ["318010"] = (3, 103755), ["237477"] = (27, 337986), ["638484"] = (61, 1638373),
        ["223210"] = (20, 584259), ["231641"] = (2, 202789),
    };

    [Fact]
    public async Task Quarter_Jun_Aug_vs_Excel()
    {
        if (!On) return;
        var c = Cfg();
        DateOnly from = new(2026, 6, 1), to = new(2026, 8, 31);
        var profit = await new SqlServerSalesProfitRepository(c.GetProperty("SqlServer").GetProperty("ConnectionString").GetString()!).GetProfitByCodeAsync(from, to);
        var data = await new SupabaseDeliveryRepository(new SupabaseReadOnlyClient(
            c.GetProperty("Supabase").GetProperty("Url").GetString()!, c.GetProperty("Supabase").GetProperty("AnonKey").GetString()!)).GetAsync(from, to);
        var r = ProfitCalculator.Calculate(data.Deliveries, data.BranchMonthlyExpense, PeriodMonths.Between(from, to), profit);
        var branchStops = data.Deliveries.Where(d => d.BranchId != null).Sum(d => d.Stops.Count);
        o.WriteLine($"App: 回数 {r.Count}(支店あり {branchStops}), 経費 {r.Expense:N0}, 粗利 {r.Profit:N0}, 損益 {r.Net:N0}   Excel: 522 / 3,195,000 / 10,608,822 / 7,438,305");
        o.WriteLine($"支店経費: {string.Join(", ", data.BranchMonthlyExpense.Select(kv => kv.Value.ToString("N0")))}");

        foreach (var x in r.Rows)
        {
            if (x.Code is null) { o.WriteLine($"  [コードなし] {x.Name} {x.Count}回"); continue; }
            if (!Quarter.TryGetValue(x.Code, out var e)) { o.WriteLine($"  [Excelに無い] {x.Name} [{x.Code}] {x.Count}回 粗利 {x.Profit:N0}"); continue; }
            if (x.Count != e.Count || x.Profit != e.Profit)
                o.WriteLine($"  [差異] {x.Name} [{x.Code}] 回数 app {x.Count} / excel {e.Count},  粗利 app {x.Profit:N0} / excel {e.Profit:N0}  {x.Warning}");
        }
        foreach (var code in Quarter.Keys.Except(r.Rows.Select(x => x.Code!)))
            o.WriteLine($"  [Excelのみ] {code} {Quarter[code]}");
    }
}
