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
}
