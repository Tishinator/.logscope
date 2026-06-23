using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.Core.Documents;

/// <summary>
/// A loaded log file: its raw lines, parsed rows, multiline events, columns, and parse statistics.
/// This is the single object the UI binds to.
/// </summary>
public sealed class LogDocument
{
    public string FilePath { get; }
    public LogProfile Profile { get; }
    public IReadOnlyList<RawLogLine> RawLines { get; }
    public IReadOnlyList<ParsedRow> Rows { get; }
    public IReadOnlyList<LogEvent> Events { get; }
    public IReadOnlyList<string> Columns { get; }
    public int ParsedCount { get; }
    public int FallbackCount { get; }

    private LogDocument(
        string filePath,
        LogProfile profile,
        IReadOnlyList<RawLogLine> rawLines,
        IReadOnlyList<ParsedRow> rows,
        IReadOnlyList<LogEvent> events,
        IReadOnlyList<string> columns,
        int parsedCount,
        int fallbackCount)
    {
        FilePath = filePath;
        Profile = profile;
        RawLines = rawLines;
        Rows = rows;
        Events = events;
        Columns = columns;
        ParsedCount = parsedCount;
        FallbackCount = fallbackCount;
    }

    public static LogDocument Load(string filePath, LogProfile profile)
    {
        var reader = new LogFileReader();
        var rawLines = reader.ReadLines(filePath).ToList();

        var (rows, parsed, fallback) = ParseRows(rawLines, profile);
        var columns = ComputeColumns(profile, rows);
        var events = BuildEvents(rows, rawLines, profile);

        return new LogDocument(filePath, profile, rawLines, rows, events, columns, parsed, fallback);
    }

    private static (List<ParsedRow> rows, int parsed, int fallback) ParseRows(
        IReadOnlyList<RawLogLine> rawLines, LogProfile profile)
    {
        switch (profile.Kind)
        {
            case LogProfileKind.Delimited:
            {
                var parser = new DelimiterParser(new DelimiterProfile(profile.Delimiter!, profile.FieldNames.ToArray()));
                var rows = parser.Parse(rawLines).ToList();
                return (rows, parser.ParsedCount, parser.FallbackCount);
            }
            case LogProfileKind.Regex:
            {
                var parser = new RegexParser(new RegexProfile(profile.Pattern!));
                var rows = parser.Parse(rawLines).ToList();
                return (rows, parser.ParsedCount, parser.FallbackCount);
            }
            default:
            {
                var parser = new RawFallbackParser();
                var rows = parser.Parse(rawLines).ToList();
                return (rows, parser.ParsedCount, parser.FallbackCount);
            }
        }
    }

    private static IReadOnlyList<string> ComputeColumns(LogProfile profile, IReadOnlyList<ParsedRow> rows)
    {
        if (profile.Kind == LogProfileKind.Delimited && profile.FieldNames.Count > 0)
            return profile.FieldNames.ToList();

        // Union of field keys in order of first appearance (handles regex groups + raw fallback rows)
        var columns = new List<string>();
        var seen = new HashSet<string>();
        foreach (var row in rows)
        {
            foreach (var key in row.Fields.Keys)
            {
                if (seen.Add(key))
                    columns.Add(key);
            }
        }

        if (columns.Count == 0)
            columns.Add("RawText");

        return columns;
    }

    private static IReadOnlyList<LogEvent> BuildEvents(
        IReadOnlyList<ParsedRow> rows, IReadOnlyList<RawLogLine> rawLines, LogProfile profile)
    {
        if (profile.MultilineNewEventPattern == null)
            return rows.Select(r => new LogEvent(r, Array.Empty<RawLogLine>())).ToList();

        var grouper = new MultilineGrouper(new MultilineRule(profile.MultilineNewEventPattern));
        return grouper.Group(rows, rawLines).ToList();
    }
}
