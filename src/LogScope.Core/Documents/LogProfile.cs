namespace LogScope.Core.Documents;

public enum LogProfileKind { Raw, Delimited, Regex }

/// <summary>
/// A unified parsing profile the UI works with: raw, delimiter-based, or regex-based,
/// optionally with a multiline "new event" rule and a friendly name.
/// </summary>
public sealed class LogProfile
{
    public string Name { get; set; } = "Untitled";
    public LogProfileKind Kind { get; }
    public string? Delimiter { get; }
    public string? Pattern { get; }
    public IReadOnlyList<string> FieldNames { get; }
    public string? MultilineNewEventPattern { get; private set; }

    private LogProfile(LogProfileKind kind, string? delimiter, string? pattern, IReadOnlyList<string> fieldNames)
    {
        Kind = kind;
        Delimiter = delimiter;
        Pattern = pattern;
        FieldNames = fieldNames;
    }

    public static LogProfile Raw() =>
        new(LogProfileKind.Raw, delimiter: null, pattern: null, fieldNames: []);

    public static LogProfile Delimited(string delimiter, IReadOnlyList<string> fieldNames) =>
        new(LogProfileKind.Delimited, delimiter, pattern: null, fieldNames);

    public static LogProfile Regex(string pattern) =>
        new(LogProfileKind.Regex, delimiter: null, pattern, fieldNames: []);

    public LogProfile WithMultiline(string newEventPattern)
    {
        MultilineNewEventPattern = newEventPattern;
        return this;
    }
}
