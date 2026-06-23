namespace LogScope.Core.Visualization;

public sealed class FlagRule
{
    // Order is preserved for back-compat with persisted integer values: FieldValue=0, Regex=1, Contains=2.
    public enum MatchKind { FieldValue, Regex, Contains }

    public MatchKind Kind { get; }

    /// <summary>The field to test. When null, Contains/Regex test every field.</summary>
    public string? FieldName { get; }
    public string MatchValue { get; }

    private FlagRule(MatchKind kind, string? fieldName, string matchValue)
    {
        Kind = kind;
        FieldName = fieldName;
        MatchValue = matchValue;
    }

    /// <summary>Exact match of a specific field's value.</summary>
    public static FlagRule ForFieldValue(string fieldName, string value) =>
        new(MatchKind.FieldValue, fieldName, value);

    /// <summary>Case-insensitive substring; scoped to <paramref name="fieldName"/> when given, else any field.</summary>
    public static FlagRule ForContains(string text, string? fieldName = null) =>
        new(MatchKind.Contains, fieldName, text);

    /// <summary>Regex; scoped to <paramref name="fieldName"/> when given, else any field.</summary>
    public static FlagRule ForRegex(string pattern, string? fieldName = null) =>
        new(MatchKind.Regex, fieldName, pattern);
}
