using System.Globalization;

namespace LogScope.Core.Sync;

/// <summary>
/// Best-effort parsing of timestamp field values across common log formats (SR-09).
/// Returns null when a value is not a recognisable timestamp so callers can warn/fallback.
/// </summary>
public static class TimestampParser
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss,fff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyyMMdd-HH:mm:ss:fff",
        "yyyyMMdd-HH:mm:ss.fff",
        "yyyyMMdd-HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss.fff",
        "yyyy/MM/dd HH:mm:ss",
        "MM/dd/yyyy HH:mm:ss",
        "HH:mm:ss.fff",
        "HH:mm:ss",
    ];

    public static DateTime? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var s = value.Trim();

        foreach (var format in Formats)
        {
            if (DateTime.TryParseExact(s, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
                return exact;
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose))
            return loose;

        return null;
    }
}
