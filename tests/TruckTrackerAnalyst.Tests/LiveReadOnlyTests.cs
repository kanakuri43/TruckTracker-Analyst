using System.Text.Json;
using TruckTrackerAnalyst.Domain;
using TruckTrackerAnalyst.Infrastructure;
using Xunit.Abstractions;

namespace TruckTrackerAnalyst.Tests;

/// <summary>実DB（読み取りのみ）との突合。TT_INTEGRATION=1 のときだけ実行。設定は Presentation/appsettings.json。</summary>
public partial class LiveReadOnlyTests(ITestOutputHelper o)
{
    static readonly bool On = Environment.GetEnvironmentVariable("TT_INTEGRATION") == "1";

    static JsonElement Cfg()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "TruckTrackerAnalyst.slnx"))) dir = Directory.GetParent(dir)!.FullName;
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "src/TruckTrackerAnalyst.Presentation/appsettings.json"))).RootElement;
    }

    [Fact]
    public async Task June2026_vs_Excel()
    {
        if (!On) return;
        var c = Cfg();
        var from = new DateOnly(2026, 6, 1); var to = new DateOnly(2026, 6, 30);

        var profit = await new SqlServerSalesProfitRepository(c.GetProperty("SqlServer").GetProperty("ConnectionString").GetString()!)
            .GetProfitByCodeAsync(from, to);
        o.WriteLine($"SQL Server: {profit.Count} 得意先, 粗利合計 {profit.Values.Sum():N0}");

        var data = await new SupabaseDeliveryRepository(new SupabaseReadOnlyClient(
            c.GetProperty("Supabase").GetProperty("Url").GetString()!, c.GetProperty("Supabase").GetProperty("AnonKey").GetString()!))
            .GetAsync(from, to);
        o.WriteLine($"Supabase: 配送 {data.Deliveries.Count} 件, 配達先 {data.Deliveries.Sum(d => d.Stops.Count)} 回, 支店 {data.BranchMonthlyExpense.Count}");
        foreach (var (id, exp) in data.BranchMonthlyExpense)
            o.WriteLine($"  支店 {id}: 月間経費 {exp:N0}, 回数 {data.Deliveries.Where(d => d.BranchId == id).Sum(d => d.Stops.Count)}");

        var r = ProfitCalculator.Calculate(data.Deliveries, data.BranchMonthlyExpense, PeriodMonths.Between(from, to), profit);
        o.WriteLine($"結果: 回数 {r.Count}, 経費 {r.Expense:N0}, 粗利 {r.Profit:N0}, 損益 {r.Net:N0}  (Excel: 196 / 1,065,000 / 3,283,378)");
        foreach (var x in r.Rows) o.WriteLine($"  {x.Name} [{x.Code}] {x.Count} 回 経費 {x.Expense:N0} 粗利 {x.Profit:N0} {x.Warning}");
    }
}

