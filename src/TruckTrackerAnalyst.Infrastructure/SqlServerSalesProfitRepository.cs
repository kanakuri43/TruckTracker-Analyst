using Dapper;
using Microsoft.Data.SqlClient;
using TruckTrackerAnalyst.Application;
using TruckTrackerAnalyst.Domain;

namespace TruckTrackerAnalyst.Infrastructure;

public sealed class SqlServerSalesProfitRepository(string connectionString) : ISalesProfitRepository
{
    // 結合は主キー(本支店コード, インデックス番号, 行番号)の先頭2列で行う。インデックス番号のみだと索引が使えず約13秒かかる（2列で約0.05秒、結果は同一）。`r`n    // 伝票日付は int(yyyyMMdd)。期間内の全得意先を取得し、コード結合はメモリ上で行う。
    private const string Sql = """
        SELECT CAST(H.得意先コード AS varchar(20)) AS Code,
               SUM(D.売価金額) - SUM(D.原価金額) AS Profit
        FROM 売上入金データ H
        LEFT JOIN 売上入金明細データ D ON H.本支店コード = D.本支店コード AND H.インデックス番号 = D.インデックス番号
        WHERE 処理区分 = 1 AND H.伝票日付 BETWEEN @from AND @to
        GROUP BY H.得意先コード
        """;

    public async Task<IReadOnlyDictionary<string, decimal>> GetProfitByCodeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var cs = new SqlConnectionStringBuilder(connectionString) { ApplicationIntent = ApplicationIntent.ReadOnly }.ConnectionString;
        await using var conn = new SqlConnection(cs);
        var rows = await conn.QueryAsync<(string Code, decimal? Profit)>(new CommandDefinition(
            ReadOnlySqlGuard.EnsureSelect(Sql),
            new { from = ToInt(from), to = ToInt(to) }, commandTimeout: 60, cancellationToken: ct));
        // 同一正規化コードは合算（0埋め等の表記ゆれ対策）
        return rows.GroupBy(r => ProfitCalculator.NormalizeCode(r.Code)!)
                   .ToDictionary(g => g.Key, g => g.Sum(r => r.Profit ?? 0));
    }

    private static int ToInt(DateOnly d) => d.Year * 10000 + d.Month * 100 + d.Day;
}

