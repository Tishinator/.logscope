using LogScope.Core.Visualization;

namespace LogScope.Core.Persistence;

/// <summary>JSON-serializable representation of a <see cref="ColorRule"/> (UR-10 / SR-10).</summary>
public sealed class ColorRuleDto
{
    public ColorRule.MatchKind Kind { get; set; }
    public string? FieldName { get; set; }
    public string MatchValue { get; set; } = string.Empty;
    public string? Background { get; set; }
    public string? FieldHighlight { get; set; }
    public int Priority { get; set; }

    public static ColorRuleDto From(ColorRule rule) => new()
    {
        Kind = rule.Kind,
        FieldName = rule.FieldName,
        MatchValue = rule.MatchValue,
        Background = rule.Background,
        FieldHighlight = rule.FieldHighlight,
        Priority = rule.Priority,
    };

    public ColorRule ToRule()
    {
        if (Kind == ColorRule.MatchKind.TimestampRange)
        {
            var parts = MatchValue.Split('|');
            var from = parts.Length > 0 && !string.IsNullOrEmpty(parts[0]) ? parts[0] : null;
            var to = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : null;
            return ColorRule.ForTimestampRange(FieldName ?? "Timestamp", from, to, Background, Priority);
        }
        return Kind switch
        {
            ColorRule.MatchKind.FieldValue => ColorRule.ForFieldValue(FieldName ?? "Level", MatchValue, Background, Priority),
            ColorRule.MatchKind.MessageContaining => ColorRule.ForMessageContaining(MatchValue, FieldHighlight, Priority),
            _ => ColorRule.ForRegex(MatchValue, Background, Priority),
        };
    }
}

/// <summary>JSON-serializable representation of a <see cref="FlagRule"/> (UR-11 / SR-10).</summary>
public sealed class FlagRuleDto
{
    public FlagRule.MatchKind Kind { get; set; }
    public string? FieldName { get; set; }
    public string MatchValue { get; set; } = string.Empty;

    public static FlagRuleDto From(FlagRule rule) => new()
    {
        Kind = rule.Kind,
        FieldName = rule.FieldName,
        MatchValue = rule.MatchValue,
    };

    public FlagRule ToRule() => Kind switch
    {
        FlagRule.MatchKind.FieldValue => FlagRule.ForFieldValue(FieldName ?? "Level", MatchValue),
        FlagRule.MatchKind.Contains => FlagRule.ForContains(MatchValue, FieldName),
        _ => FlagRule.ForRegex(MatchValue, FieldName),
    };
}

/// <summary>Out-of-the-box rules used when the user has not customised their own.</summary>
public static class DefaultRuleSets
{
    public static List<ColorRuleDto> ColorRules() =>
    [
        new() { Kind = ColorRule.MatchKind.Regex, MatchValue = @"\b(ERROR|FATAL|FAIL|EXCEPTION|ASSERT)\b", Background = "#5A1F1F", Priority = 10 },
        new() { Kind = ColorRule.MatchKind.Regex, MatchValue = @"\b(WARN|WARNING|TIMEOUT)\b", Background = "#5A4A1F", Priority = 5 },
    ];

    public static List<FlagRuleDto> FlagRules() =>
    [
        new() { Kind = FlagRule.MatchKind.Regex, MatchValue = @"\b(ERROR|FATAL|FAIL|ASSERT|TIMEOUT|EXCEPTION|WARN)\b" },
    ];
}
