using LogScope.Core.Documents;

namespace LogScope.Core.Persistence;

/// <summary>JSON-serializable representation of a <see cref="LogProfile"/>.</summary>
public sealed class LogProfileDto
{
    public string Name { get; set; } = "Untitled";
    public LogProfileKind Kind { get; set; }
    public string? Delimiter { get; set; }
    public string? Pattern { get; set; }
    public List<string> FieldNames { get; set; } = [];
    public string? MultilineNewEventPattern { get; set; }
    public Dictionary<string, FieldSemanticType> FieldTypes { get; set; } = new();
    public List<string>? LevelOrder { get; set; }

    public static LogProfileDto From(LogProfile profile) => new()
    {
        Name = profile.Name,
        Kind = profile.Kind,
        Delimiter = profile.Delimiter,
        Pattern = profile.Pattern,
        FieldNames = profile.FieldNames.ToList(),
        MultilineNewEventPattern = profile.MultilineNewEventPattern,
        FieldTypes = new Dictionary<string, FieldSemanticType>(profile.FieldTypes),
        LevelOrder = profile.LevelOrder.ToList(),
    };

    public LogProfile ToProfile()
    {
        var profile = Kind switch
        {
            LogProfileKind.Delimited => LogProfile.Delimited(Delimiter ?? "|", FieldNames),
            LogProfileKind.Regex => LogProfile.Regex(Pattern ?? ".*"),
            _ => LogProfile.Raw(),
        };
        profile.Name = Name;
        if (!string.IsNullOrEmpty(MultilineNewEventPattern))
            profile.WithMultiline(MultilineNewEventPattern);
        if (FieldTypes.Count > 0)
            profile.FieldTypes = new Dictionary<string, FieldSemanticType>(FieldTypes, StringComparer.OrdinalIgnoreCase);
        if (LevelOrder is { Count: > 0 })
            profile.LevelOrder = LevelOrder.ToList();
        return profile;
    }
}
