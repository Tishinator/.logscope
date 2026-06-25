using System.Text;
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

    /// <summary>True when the file had more lines than the load cap (SR-05 memory guard).</summary>
    public bool Truncated { get; }

    /// <summary>The encoding the file was read with (SR-04).</summary>
    public string EncodingName { get; }

    /// <summary>A warning about encoding fallback, if any (SR-04/SR-07).</summary>
    public string? EncodingWarning { get; }

    /// <summary>Default cap on parsed lines so a 2 GB file does not exhaust memory (SR-05).</summary>
    public const int DefaultMaxLines = 500_000;

    private LogDocument(
        string filePath,
        LogProfile profile,
        IReadOnlyList<RawLogLine> rawLines,
        IReadOnlyList<ParsedRow> rows,
        IReadOnlyList<LogEvent> events,
        IReadOnlyList<string> columns,
        int parsedCount,
        int fallbackCount,
        bool truncated,
        string encodingName,
        string? encodingWarning)
    {
        FilePath = filePath;
        Profile = profile;
        RawLines = rawLines;
        Rows = rows;
        Events = events;
        Columns = columns;
        ParsedCount = parsedCount;
        FallbackCount = fallbackCount;
        Truncated = truncated;
        EncodingName = encodingName;
        EncodingWarning = encodingWarning;
    }

    public static LogDocument Load(string filePath, LogProfile profile) =>
        Load(filePath, profile, encoding: null, maxLines: DefaultMaxLines);

    public static LogDocument Load(string filePath, LogProfile profile, Encoding? encoding, int maxLines)
    {
        var detection = EncodingDetector.DetectFromFile(filePath);
        var chosen = encoding ?? detection.Encoding;

        var reader = new LogFileReader();
        // Read one extra line to detect truncation without a second full pass.
        var rawLines = reader.ReadLines(filePath, chosen).Take(maxLines + 1).ToList();
        bool truncated = rawLines.Count > maxLines;
        if (truncated)
            rawLines = rawLines.Take(maxLines).ToList();

        var (rows, parsed, fallback) = ParseRows(rawLines, profile);
        var columns = ComputeColumns(profile, rows);
        var events = BuildEvents(rows, rawLines, profile);

        return new LogDocument(filePath, profile, rawLines, rows, events, columns, parsed, fallback,
            truncated, detection.EncodingName, detection.IsFallback ? detection.Warning : null);
    }

    /// <summary>
    /// Parses a batch of raw lines with the given profile (used for incremental streaming appends).
    /// </summary>
    public static (List<ParsedRow> Rows, int Parsed, int Fallback) ParseLines(
        IReadOnlyList<RawLogLine> rawLines, LogProfile profile) => ParseRows(rawLines, profile);

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
