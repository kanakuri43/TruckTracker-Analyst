using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Tests;

public class PeriodMonthsTests
{
    [Theory]
    [InlineData("2026-06-01", "2026-08-31", 3)]
    [InlineData("2026-06-01", "2026-06-30", 1)]
    public void FullMonths(string from, string to, int expected) =>
        Assert.Equal(expected, PeriodMonths.Between(DateOnly.Parse(from), DateOnly.Parse(to)));

    [Fact]
    public void PartialMonth_IsProrated() =>
        Assert.Equal(17m / 31, PeriodMonths.Between(new(2026, 10, 15), new(2026, 10, 31)));
}

/// <summary>Excel「秋田配送- 得意先別.xlsx」6月得意先別シートの値を期待値にした回帰テスト。</summary>
public class ProfitCalculatorJuneTests
{
    static readonly Guid Akita = Guid.NewGuid();

    // (配送先名, 得意先コード, 回数) — Excel A:C 列
    static readonly (string Name, string? Code, int Count)[] June =
    [
        ("ケンダン", "573524", 1), ("三傳商事", null, 1), ("三戸印刷所", "235351", 12), ("三浦印刷", "622792", 5),
        ("八郎潟印刷", "754005", 12), ("塚田印刷", "235551", 15), ("新田紙函", "225941", 1), ("工藤平版", "862665", 5),
        ("高野写真", "227456", 7), ("東北印刷", "732647", 8), ("とみや", "628002", 7), ("フロムエー", "643784", 30),
        ("三森印刷", "620433", 12), ("丸栄商事", "635161", 2), ("全農", "235515", 1), ("塚清", "524351", 3),
        ("成文社", "621231", 1), ("木曽印刷", "633507", 9), ("本間印刷所", "221436", 1), ("松本印刷", "631450", 12),
        ("桑原", "631818", 1), ("秋田情報", "236648", 11), ("辻源", "8622535", 1), ("ワークセンター", "318010", 1),
        ("秋田協同", "237477", 10), ("秋田活版", "638484", 20), ("第一印刷", "223210", 4), ("近藤とっぱん", null, 3),
    ];

    // Excel I:K 列（販売管理の粗利）。221436 は売上なし
    static readonly Dictionary<string, decimal> Profit = new()
    {
        ["223210"] = 14362, ["227456"] = 459631, ["235351"] = 42295, ["235515"] = -176, ["235551"] = 98942,
        ["236648"] = 43613, ["237477"] = 273015, ["318010"] = 29257, ["524351"] = 7861, ["573524"] = 2114,
        ["620433"] = 380829, ["621231"] = 14791, ["622792"] = 110982, ["628002"] = 31531, ["631450"] = 111337,
        ["631818"] = 1785, ["633507"] = 34393, ["635161"] = 18570, ["638484"] = 530677, ["643784"] = 243752,
        ["732647"] = 280820, ["754005"] = 426958, ["862665"] = 109014, ["8622535"] = 2663, ["225941"] = 14362,
    };

    static ProfitReport Run(CountMode mode = CountMode.Count) => ProfitCalculator.Calculate(
        June.SelectMany(j => Enumerable.Repeat(new Delivery(Akita, [new Stop(j.Name, j.Code)]), j.Count)),
        new Dictionary<Guid, decimal> { [Akita] = 1_065_000 }, 1m, Profit, mode);

    [Fact]
    public void Totals_MatchExcel()
    {
        var r = Run();
        Assert.Equal(196, r.Count);
        Assert.Equal(1_065_000, Math.Round(r.Expense));
        Assert.Equal(3_283_378, r.Profit);
        // Excel の営業利益合計(2,245,546)はエラー行の経費を除外しているため、アプリは 粗利合計 − 経費合計 とする
        Assert.Equal(2_218_378, Math.Round(r.Net));
    }

    [Fact]
    public void Row_MatchesExcel()
    {
        var mito = Run().Rows.Single(x => x.Code == "235351");
        Assert.Equal(65_204, Math.Round(mito.Expense));   // 12回 × 5,434
        Assert.Equal(-22_909, Math.Round(mito.Net!.Value));
    }

    [Fact]
    public void Warnings()
    {
        var rows = Run().Rows;
        Assert.Equal(Warning.NoCode, rows.Single(x => x.Name == "三傳商事").Warning);
        Assert.Null(rows.Single(x => x.Name == "近藤とっぱん").Profit);
        Assert.Equal(Warning.NoSales, rows.Single(x => x.Code == "221436").Warning);
    }

    [Fact]
    public void SameCode_IsMerged_AndLeadingZerosIgnored()
    {
        var b = Guid.NewGuid();
        var r = ProfitCalculator.Calculate(
            [new Delivery(b, [new Stop("A", "0123")]), new Delivery(b, [new Stop("A別名", "123")])],
            new Dictionary<Guid, decimal> { [b] = 200 }, 1m, new Dictionary<string, decimal> { ["123"] = 500 });
        var row = Assert.Single(r.Rows);
        Assert.Equal(2, row.Count);
        Assert.Equal(200, row.Expense);
        Assert.Equal(300, row.Net);
    }

    [Fact]
    public void NetMode_SplitsByStopsInDelivery()
    {
        var b = Guid.NewGuid();
        var r = ProfitCalculator.Calculate(
            [new Delivery(b, [new Stop("A", "1"), new Stop("B", "2")])],
            new Dictionary<Guid, decimal> { [b] = 100 }, 1m, new Dictionary<string, decimal>(), CountMode.Net);
        Assert.All(r.Rows, x => { Assert.Equal(0.5m, x.Count); Assert.Equal(50, x.Expense); });
    }

    [Fact]
    public void MissingCode_IsFilledFromSameNameDestination()
    {
        var b = Guid.NewGuid();
        var r = ProfitCalculator.Calculate(
            [new Delivery(b, [new Stop("A", "100")]), new Delivery(b, [new Stop("A", null)]), new Delivery(b, [new Stop("B", null)])],
            new Dictionary<Guid, decimal> { [b] = 300 }, 1m, new Dictionary<string, decimal> { ["100"] = 1000 });
        var a = r.Rows.Single(x => x.Name == "A");
        Assert.Equal(2, a.Count);                         // コードなしの記録も A(100) に合算
        Assert.Equal(Warning.None, a.Warning);
        Assert.Equal(Warning.NoCode, r.Rows.Single(x => x.Name == "B").Warning);   // 補完できない名前は未設定のまま
    }
}

public class RowOrderTests
{
    [Fact]
    public void Rows_AreSortedByNetAscending_NoCodeLast()
    {
        var b = Guid.NewGuid();
        var r = ProfitCalculator.Calculate(
            [new Delivery(b, [new Stop("黒字", "1"), new Stop("赤字", "2"), new Stop("コードなし", null)])],
            new Dictionary<Guid, decimal> { [b] = 300 }, 1m, new Dictionary<string, decimal> { ["1"] = 1000, ["2"] = 0 });
        Assert.Equal(["赤字", "黒字", "コードなし"], r.Rows.Select(x => x.Name));
    }
}

public class DistanceWeightTests
{
    static readonly Guid B = Guid.NewGuid();

    static ProfitReport Run(decimal share, params Stop[] stops) => ProfitCalculator.Calculate(
        stops.Select(s => new Delivery(B, [s])), new Dictionary<Guid, decimal> { [B] = 1000 }, 1m,
        new Dictionary<string, decimal>(), distanceShare: share);

    static decimal Exp(ProfitReport r, string name) => r.Rows.Single(x => x.Name == name).Expense;

    [Fact]
    public void Share50_SplitsHalfByCount_HalfByCountTimesKm()
    {
        // A 10km ×1回, B 30km ×1回: 回数分 500 を 1:1、距離分 500 を 10:30
        var r = Run(0.5m, new Stop("A", "1", 10), new Stop("B", "2", 30));
        Assert.Equal(250 + 125, Exp(r, "A"));
        Assert.Equal(250 + 375, Exp(r, "B"));
        Assert.Equal(1000, r.Expense);
        Assert.Equal(10, r.Rows.Single(x => x.Name == "A").Km);
    }

    [Fact]
    public void Share0_IsSameAsCountOnly() =>
        Assert.Equal(500, Exp(Run(0, new Stop("A", "1", 10), new Stop("B", "2", 90)), "A"));

    [Fact]
    public void MissingKm_FilledBySameName_ThenAverage()
    {
        // A(null) は同名の A=10 で補完。C は座標なし → 座標ありの記録の平均 (10+40)/2 = 25
        var r = Run(1, new Stop("A", "1", 10), new Stop("A", "1", null), new Stop("B", "2", 40), new Stop("C", null, null));
        Assert.Equal(10, r.Rows.Single(x => x.Name == "A").Km);
        Assert.Equal(25, r.Rows.Single(x => x.Name == "C").Km);
        Assert.Equal(1000m * 20 / 85, Exp(r, "A"));
        Assert.Equal(1000m * 25 / 85, Exp(r, "C"));
    }

    [Fact]
    public void AllZeroKm_FallsBackToCount() =>
        Assert.Equal(500, Exp(Run(1, new Stop("A", "1", 0), new Stop("B", "2", 0)), "A"));

    [Fact]
    public void Haversine_AkitaToTokyo_About450km() =>
        Assert.InRange(Geo.Km(39.7186, 140.1024, 35.6812, 139.7671), 445m, 455m);
}
