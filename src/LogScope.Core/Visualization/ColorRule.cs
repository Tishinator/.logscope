namespace LogScope.Core.Visualization;

public sealed class ColorRule
{
    public enum MatchKind { FieldValue, MessageContaining, Regex }

    public MatchKind Kind { get; }
    public string? FieldName { get; }
    public string MatchValue { get; }
    public string? Background { get; }
    public string? FieldHighlight { get; }
    public int Priority { get; }

    private ColorRule(MatchKind kind, string? fieldName, string matchValue, string? background, string? fieldHighlight, int priority)
    {
        Kind = kind;
        FieldName = fieldName;
        MatchValue = matchValue;
        Background = background;
        FieldHighlight = fieldHighlight;
        Priority = priority;
    }

    public static ColorRule ForFieldValue(string fieldName, string value, string? background = null, int priority = 0) =>
        new(MatchKind.FieldValue, fieldName, value, background, fieldHighlight: null, priority);

    public static ColorRule ForMessageContaining(string text, string? fieldHighlight = null, int priority = 0) =>
        new(MatchKind.MessageContaining, fieldName: null, text, background: null, fieldHighlight, priority);

    public static ColorRule ForRegex(string pattern, string? background = null, int priority = 0) =>
        new(MatchKind.Regex, fieldName: null, pattern, background, fieldHighlight: null, priority);
}
