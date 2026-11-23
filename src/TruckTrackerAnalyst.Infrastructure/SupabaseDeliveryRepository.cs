using System.Text.Json;
using TruckTrackerAnalyst.Application;
using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Infrastructure;

public sealed class SupabaseDeliveryRepository(SupabaseReadOnlyClient client) : IDeliveryRepository
{
    private const int ReportChunk = 100;   // in.(...) の URL 長対策

    public async Task<DeliveryData> GetAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var reports = await client.GetAllAsync(
            $"reports?select=id,trucks(branch_id,branches(id,monthly_expense,lat,lng))&date=gte.{from:yyyy-MM-dd}&date=lte.{to:yyyy-MM-dd}", ct);

        var branchOf = new Dictionary<string, Guid?>();
        var branchPos = new Dictionary<string, (double Lat, double Lng)?>();   // report_id → 支店座標
        var expense = new Dictionary<Guid, decimal>();
        foreach (var r in reports)
        {
            var branch = Obj(Obj(r, "trucks"), "branches");
            Guid? id = branch is { } b && b.GetProperty("id").GetGuid() is var g ? g : null;
            if (id is { } bid) expense[bid] = branch!.Value.GetProperty("monthly_expense").GetDecimal();
            branchOf[r.GetProperty("id").GetString()!] = id;
            branchPos[r.GetProperty("id").GetString()!] = Pos(branch);
        }

        // 到着記録あり（配達完了）のみ。TruckTracker の CSV 出力と同条件。
        var stopsByReport = new Dictionary<string, List<Stop>>();
        foreach (var chunk in branchOf.Keys.Chunk(ReportChunk))
        {
            var rows = await client.GetAllAsync(
                "stop_records?select=report_id,destination_name,course_stops(destinations(sales_customer_code,lat,lng))" +
                $"&arrived_at=not.is.null&report_id=in.({string.Join(',', chunk)})&order=report_id", ct);
            foreach (var s in rows)
            {
                var dest = Obj(Obj(s, "course_stops"), "destinations");
                var code = Obj(dest, "sales_customer_code");
                var rid = s.GetProperty("report_id").GetString()!;
                var km = branchPos[rid] is { } bp && Pos(dest) is { } dp ? Geo.Km(bp.Lat, bp.Lng, dp.Lat, dp.Lng) : (decimal?)null;
                var stop = new Stop(s.GetProperty("destination_name").GetString() ?? "（不明）",
                                    code is { ValueKind: JsonValueKind.String } c ? c.GetString() : null, km);
                (stopsByReport.TryGetValue(rid, out var l) ? l : stopsByReport[rid] = []).Add(stop);
            }
        }

        var deliveries = stopsByReport.Select(kv => new Delivery(branchOf[kv.Key], kv.Value)).ToList();
        return new DeliveryData(deliveries, expense);
    }

    private static (double Lat, double Lng)? Pos(JsonElement? e) =>
        Obj(e, "lat") is { } lat && Obj(e, "lng") is { } lng ? (lat.GetDouble(), lng.GetDouble()) : null;

    private static JsonElement? Obj(JsonElement? e, string name) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;
}
