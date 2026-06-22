namespace LogScope.Core.Search;

public sealed record SearchQuery(
    string Text,
    bool CaseSensitive = false,
    bool IsRegex = false,
    bool WholeWord = false,
    string? FieldName = null);
