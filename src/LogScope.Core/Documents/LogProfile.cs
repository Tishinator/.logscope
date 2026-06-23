namespace LogScope.Core.Documents;

public enum LogProfileKind { Raw, Delimited, Regex }

/// <summary>
/// Semantic role of a parsed field (UR-06). Drives behavior: a Timestamp field can be sorted
/// chronologically, a Level field sorts by configured severity order, etc.
/// </summary>
public enum FieldSemanticType
{
    Generic,
    Timestamp,
    Level,
    Module,
    Message,
    Thread,
    DeviceId,
    TestCase,
    RunId,
    Result,
}

/// <summary>
/// A unified parsing profile the UI works with: raw, delimiter-based, or regex-based,
/// optionally with a multiline "new event" rule, semantic field types, and a custom
/// log-level severity ordering.
/// </summary>
public sealed class LogProfile
{
    /// <summary>Standard severity order, least to most severe.</summary>
    public static readonly IReadOnlyList<string> StandardLevelOrder =
        ["TRACE", "DEBUG", "INFO", "WARN", "ERROR", "FATAL"];

    public string Name { get; set; } = "Untitled";
    public LogProfileKind Kind { get; }
    public string? Delimiter { get; }
    public string? Pattern { get; }
    public IReadOnlyList<string> FieldNames { get; }
    public string? MultilineNewEventPattern { get; private set; }

    /// <summary>Field name → semantic role (UR-06). Unassigned fields are <see cref="FieldSemanticType.Generic"/>.</summary>
    public Dictionary<string, FieldSemanticType> FieldTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Custom log-level severity order, least to most severe (UR-06).</summary>
    public List<string> LevelOrder { get; set; } = StandardLevelOrder.ToList();

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

    public void SetFieldType(string fieldName, FieldSemanticType type) => FieldTypes[fieldName] = type;

    public FieldSemanticType TypeOf(string fieldName) =>
        FieldTypes.TryGetValue(fieldName, out var t) ? t : FieldSemanticType.Generic;

    public string? TimestampField => FieldOfType(FieldSemanticType.Timestamp);
    public string? LevelField => FieldOfType(FieldSemanticType.Level);
    public string? MessageField => FieldOfType(FieldSemanticType.Message);

    private string? FieldOfType(FieldSemanticType type) =>
        FieldTypes.FirstOrDefault(kv => kv.Value == type).Key;

    /// <summary>
    /// Severity rank of a level value per <see cref="LevelOrder"/> (case-insensitive).
    /// Unknown levels rank after all known ones so they sort last.
    /// </summary>
    public int LevelRank(string value)
    {
        for (int i = 0; i < LevelOrder.Count; i++)
            if (string.Equals(LevelOrder[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        return LevelOrder.Count;
    }
}
