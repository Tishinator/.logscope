namespace LogScope.Core.Filtering;

public sealed class FilterRule
{
    public enum RuleKind { IncludeFieldValue, ExcludeFieldValue, IncludeText, ExcludeText, IncludeRegex, ExcludeRegex }

    public RuleKind Kind { get; }
    public string? FieldName { get; }
    public string Value { get; }
    public bool CaseSensitive { get; }

    private FilterRule(RuleKind kind, string? fieldName, string value, bool caseSensitive)
    {
        Kind = kind;
        FieldName = fieldName;
        Value = value;
        CaseSensitive = caseSensitive;
    }

    public static FilterRule IncludeWhere(string fieldName, string value) =>
        new(RuleKind.IncludeFieldValue, fieldName, value, caseSensitive: true);

    public static FilterRule ExcludeWhere(string fieldName, string value) =>
        new(RuleKind.ExcludeFieldValue, fieldName, value, caseSensitive: true);

    public static FilterRule IncludeContainingText(string text, bool caseSensitive) =>
        new(RuleKind.IncludeText, fieldName: null, text, caseSensitive);

    public static FilterRule IncludeMatchingRegex(string pattern, string? fieldName = null) =>
        new(RuleKind.IncludeRegex, fieldName, pattern, caseSensitive: true);

    public static FilterRule ExcludeMatchingRegex(string pattern, string? fieldName = null) =>
        new(RuleKind.ExcludeRegex, fieldName, pattern, caseSensitive: true);
}
