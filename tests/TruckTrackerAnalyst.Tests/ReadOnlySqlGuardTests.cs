using TruckTrackerAnalyst.Infrastructure;

namespace TruckTrackerAnalyst.Tests;

public class ReadOnlySqlGuardTests
{
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("  select a from t where x = @x;")]
    [InlineData("-- c\nWITH c AS (SELECT 1 AS a) SELECT a FROM c")]
    [InlineData("SELECT 更新日 FROM 売上入金データ")]   // 列名の部分一致は許可
    public void Allows(string sql) => Assert.Equal(sql, ReadOnlySqlGuard.EnsureSelect(sql));

    [Theory]
    [InlineData("UPDATE t SET a = 1")]
    [InlineData("DELETE FROM t")]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("SELECT 1; DROP TABLE t")]
    [InlineData("SELECT * INTO t2 FROM t")]
    [InlineData("EXEC sp_who")]
    [InlineData("/* SELECT */ DELETE FROM t")]
    [InlineData("SELECT 1 FROM t WHERE a IN (SELECT 1); EXEC xp_cmdshell 'dir'")]
    public void Rejects(string sql) => Assert.Throws<InvalidOperationException>(() => ReadOnlySqlGuard.EnsureSelect(sql));
}
