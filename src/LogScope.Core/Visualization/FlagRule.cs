namespace LogScope.Core.Visualization;

public sealed class FlagRule
{
    public enum MatchKind { FieldValue, Regex }

    public MatchKind Kind { get; }
    public string? FieldName { get; }
    public string MatchValue { get; }

    private FlagRule(MatchKind kind, string? fieldName, string matchValue)
    {
        Kind = kind;
        FieldName = fieldName;
        MatchValue = matchValue;
    }

    public static FlagRule ForFieldValue(string fieldName, string value) =>
        new(MatchKind.FieldValue, fieldName, value);

    public static FlagRule ForRegex(string pattern) =>
        new(MatchKind.Regex, fieldName: null, pattern);
}
