using System.Text;
using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Application;

public static class WarningText
{
    /// <summary>備考欄の文言。1行に収まる短さで、原因と計算上の扱いを書く（CSVにも同じ文を出す）。</summary>
    public static string Of(CustomerProfit r) =>
        string.Join("／", new[] { OfWarning(r), r.SignFlips ? "回数按分と距離按分で黒字・赤字が逆転" : "" }.Where(s => s != ""));

    private static string OfWarning(CustomerProfit r) => r.Warning switch
    {
        Warning.NoCode =>
            "得意先コード未設定のため粗利算出不可（経費のみ計上）",
        Warning.NoSales =>
            "期間内売上なし（粗利0円で計算）",
        _ => "",
    };
}

/// <summary>得意先別営業利益の CSV 整形。金額は円・整数（四捨五入）、粗利算出不可は空欄。</summary>
public static class ProfitCsv
{
    public static string Build(ProfitReport r)
    {
        var sb = new StringBuilder("得意先名,得意先コード,距離(km),回数,期間経費,粗利,営業利益,備考\r\n");
        foreach (var x in r.Rows)
            sb.AppendLine(Line(x.Name, x.Code, x.Km, x.Count, x.Expense, x.Profit, x.Net, WarningText.Of(x)));
        sb.AppendLine(Line("合計", null, null, r.Count, r.Expense, r.Profit, r.Net, ""));
        return sb.ToString();
    }

    private static string Line(string name, string? code, decimal? km, decimal count, decimal expense, decimal? profit, decimal? net, string note) =>
        string.Join(',', Esc(name), Esc(code), km?.ToString("0.0") ?? "", count.ToString("0.##"), Yen(expense), Yen(profit), Yen(net), Esc(note));

    private static string Yen(decimal? v) => v is { } d ? Math.Round(d, MidpointRounding.AwayFromZero).ToString("0") : "";

    private static string Esc(string? s) =>
        s is null ? "" : s.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}



