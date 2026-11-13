using System.Diagnostics;
using TruckTrackerAnalyst.Infrastructure;

namespace TruckTrackerAnalyst.Tests;

public partial class LiveReadOnlyTests
{
    [Fact]
    public async Task Timing_Oct2026()
    {
        if (!On) return;
        var c = Cfg();
        var from = new DateOnly(2026, 10, 1); var to = new DateOnly(2026, 10, 31);
        var sw = Stopwatch.StartNew();

        var sql = new SqlServerSalesProfitRepository(c.GetProperty("SqlServer").GetProperty("ConnectionString").GetString()!);
        await sql.GetProfitByCodeAsync(from, to);
        o.WriteLine($"SQL Server 1回目: {sw.ElapsedMilliseconds} ms"); sw.Restart();
        await sql.GetProfitByCodeAsync(from, to);
        o.WriteLine($"SQL Server 2回目: {sw.ElapsedMilliseconds} ms"); sw.Restart();

        var client = new SupabaseReadOnlyClient(c.GetProperty("Supabase").GetProperty("Url").GetString()!, c.GetProperty("Supabase").GetProperty("AnonKey").GetString()!);
        var reports = await client.GetAllAsync($"reports?select=id&date=gte.{from:yyyy-MM-dd}&date=lte.{to:yyyy-MM-dd}");
        o.WriteLine($"Supabase reports {reports.Count}件: {sw.ElapsedMilliseconds} ms"); sw.Restart();
        await client.GetAllAsync("branches?select=id");
        o.WriteLine($"Supabase 1リクエスト(branches): {sw.ElapsedMilliseconds} ms"); sw.Restart();
        await new SupabaseDeliveryRepository(client).GetAsync(from, to);
        o.WriteLine($"Supabase 全体: {sw.ElapsedMilliseconds} ms");
    }
}
