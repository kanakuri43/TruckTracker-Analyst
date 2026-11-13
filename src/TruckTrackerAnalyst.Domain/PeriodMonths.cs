namespace TruckTrackerAnalyst.Domain;

/// <summary>集計期間の月数。端数月は日割り（例: 6/1〜8/31 → 3、10/15〜10/31 → 17/31）。</summary>
public static class PeriodMonths
{
    public static decimal Between(DateOnly from, DateOnly to)
    {
        if (to < from) throw new ArgumentException("to は from 以降である必要があります。");
        decimal months = 0;
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
        {
            var monthEnd = m.AddMonths(1).AddDays(-1);
            var start = from > m ? from : m;
            var end = to < monthEnd ? to : monthEnd;
            if (end >= start)
                months += (decimal)(end.DayNumber - start.DayNumber + 1) / DateTime.DaysInMonth(m.Year, m.Month);
        }
        return months;
    }
}
