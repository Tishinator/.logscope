using LogScope.Core.Parsing;
using LogScope.Core.Sync;

namespace LogScope.Core.Filtering;

/// <summary>
/// Filters rows by a parsed timestamp field falling within an optional [from, to] range (UR-08).
/// Rows whose timestamp field is missing or unparseable are excluded when a bound is set.
/// </summary>
public static class TimeRangeFilter
{
    public static IEnumerable<ParsedRow> Apply(
        IEnumerable<ParsedRow> rows, string? timestampField, DateTime? from, DateTime? to)
    {
        if (timestampField == null || (from == null && to == null))
            return rows;

        return rows.Where(row =>
        {
            if (!row.Fields.TryGetValue(timestampField, out var value))
                return false;
            var ts = TimestampParser.TryParse(value);
            if (ts == null) return false;
            if (from != null && ts < from) return false;
            if (to != null && ts > to) return false;
            return true;
        });
    }
}
