namespace LogScope.Core.Visualization;

public sealed class ColorRule
{
    public enum MatchKind { FieldValue, MessageContaining, Regex, TimestampRange }

    public MatchKind Kind { get; }
    public string? FieldName { get; }
    public string MatchValue { get; }
    public string? Background { get; }
    public string? FieldHighlight { get; }
    public int Priority { get; }

    // Parsed from MatchValue for TimestampRange rules ("from|to").
    public DateTime? TimeFrom { get; }
    public DateTime? TimeTo { get; }

    private ColorRule(MatchKind kind, string? fieldName, string matchValue, string? background, string? fieldHighlight, int priority)
    {
        Kind = kind;
        FieldName = fieldName;
        MatchValue = matchValue;
        Background = background;
        FieldHighlight = fieldHighlight;
        Priority = priority;

        if (kind == MatchKind.TimestampRange)
        {
            var parts = matchValue.Split('|');
            if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var from)) TimeFrom = from;
            if (parts.Length >= 2 && DateTime.TryParse(parts[1], out var to)) TimeTo = to;
        }
    }

    public static ColorRule ForFieldValue(string fieldName, string value, string? background = null, int priority = 0) =>
        new(MatchKind.FieldValue, fieldName, value, background, fieldHighlight: null, priority);

    public static ColorRule ForMessageContaining(string text, string? fieldHighlight = null, int priority = 0) =>
        new(MatchKind.MessageContaining, fieldName: null, text, background: null, fieldHighlight, priority);

    public static ColorRule ForRegex(string pattern, string? background = null, int priority = 0) =>
        new(MatchKind.Regex, fieldName: null, pattern, background, fieldHighlight: null, priority);

    /// <summary>
    /// Highlights rows whose Timestamp field (FieldName) falls within [from, to].
    /// MatchValue format: "from|to" as ISO-8601 strings; either side may be blank for open-ended.
    /// </summary>
    public static ColorRule ForTimestampRange(string fieldName, string? from, string? to, string? background = null, int priority = 0) =>
        new(MatchKind.TimestampRange, fieldName, $"{from}|{to}", background, fieldHighlight: null, priority);
}
