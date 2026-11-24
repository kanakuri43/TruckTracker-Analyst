namespace TruckTrackerAnalyst.Domain;

public static class Geo
{
    /// <summary>2点間の直線距離(km)。Haversine 式（地球半径 6371km）。</summary>
    public static decimal Km(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * r / 2), 2)
              + Math.Cos(lat1 * r) * Math.Cos(lat2 * r) * Math.Pow(Math.Sin((lng2 - lng1) * r / 2), 2);
        return (decimal)(2 * 6371 * Math.Asin(Math.Sqrt(a)));
    }

    /// <summary>
    /// 1コースの走行距離（支店→到着順の各配達先→支店）を配達先ごとに按分する。points は到着順、座標なしは null（経路から除外し結果も null）。
    /// 距離帯方式: 支店からの直線距離で帯に分け、各帯をその帯より遠い（同じを含む）配達先で等分 → 走行距離に合わせて拡大。
    /// 回る順番に左右されない。合計 = 走行距離。
    /// </summary>
    public static decimal?[] RouteShares((double Lat, double Lng) branch, IReadOnlyList<(double Lat, double Lng)?> points)
    {
        var result = new decimal?[points.Count];
        var stops = Enumerable.Range(0, points.Count).Where(i => points[i] is not null)
            .Select(i => (Index: i, P: points[i]!.Value)).ToList();
        if (stops.Count == 0) return result;

        decimal route = 0;
        var prev = branch;
        foreach (var s in stops) { route += Km(prev.Lat, prev.Lng, s.P.Lat, s.P.Lng); prev = s.P; }
        route += Km(prev.Lat, prev.Lng, branch.Lat, branch.Lng);

        var byDist = stops.Select(s => (s.Index, D: Km(branch.Lat, branch.Lng, s.P.Lat, s.P.Lng))).OrderBy(s => s.D).ToList();
        var farthest = byDist[^1].D;
        decimal acc = 0, below = 0;
        for (var k = 0; k < byDist.Count; k++)
        {
            acc += (byDist[k].D - below) / (byDist.Count - k);   // この帯を走るのは k 番目以降（より遠い）の配達先
            below = byDist[k].D;
            result[byDist[k].Index] = farthest == 0 ? route / stops.Count : acc * route / farthest;
        }
        return result;
    }
}
