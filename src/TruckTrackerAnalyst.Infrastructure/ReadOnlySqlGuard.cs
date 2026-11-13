using System.Text.RegularExpressions;

namespace TruckTrackerAnalyst.Infrastructure;

/// <summary>SQL Server へ送る文を SELECT（または WITH ... SELECT）の単一文に限定する。sa 接続のため DB 権限に頼れない。</summary>
public static partial class ReadOnlySqlGuard
{
    public static string EnsureSelect(string sql)
    {
        var s = Comments().Replace(sql, " ").Trim().TrimEnd(';').Trim();
        if (!StartsOk().IsMatch(s)) throw new InvalidOperationException("読み取り専用: SELECT 以外は実行できません。");
        if (s.Contains(';')) throw new InvalidOperationException("読み取り専用: 複数文は実行できません。");
        if (Forbidden().IsMatch(s)) throw new InvalidOperationException("読み取り専用: 更新系キーワードを含むSQLは実行できません。");
        return sql;
    }

    [GeneratedRegex(@"--[^\r\n]*|/\*.*?\*/", RegexOptions.Singleline)] private static partial Regex Comments();
    [GeneratedRegex(@"^(SELECT|WITH)\b", RegexOptions.IgnoreCase)] private static partial Regex StartsOk();
    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|GRANT|REVOKE|INTO|OPENROWSET|OPENQUERY|XP_\w+)\b", RegexOptions.IgnoreCase)] private static partial Regex Forbidden();
}
