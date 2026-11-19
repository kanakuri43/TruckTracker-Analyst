using TruckTrackerAnalyst.Application;
using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Tests;

public class ProfitCsvTests
{
    [Fact]
    public void Build_FormatsRowsAndTotal()
    {
        var report = new ProfitReport([
            new CustomerProfit("A,社 \"本店\"", "100", 2, 1000.5m, 3000, Warning.None),
            new CustomerProfit("近藤", null, 3, 500, null, Warning.NoCode),
        ]);
        var lines = ProfitCsv.Build(report).TrimEnd().Split("\r\n");
        Assert.Equal("得意先名,得意先コード,回数,期間経費,粗利,営業利益,備考", lines[0]);
        Assert.Equal("\"A,社 \"\"本店\"\"\",100,2,1001,3000,2000,", lines[1]);   // 各値を四捨五入（1000.5→1001, 1999.5→2000）
        Assert.Equal("近藤,,3,500,,,得意先コード未設定のため粗利算出不可（経費のみ計上）", lines[2]);
        Assert.StartsWith("合計,,5,1501,3000,", lines[3]);
    }
}



