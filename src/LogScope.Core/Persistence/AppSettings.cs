namespace LogScope.Core.Persistence;

/// <summary>A saved filter preset (UR-08). ProfileScope=null means global; a name means profile-scoped.</summary>
public sealed record FilterPreset(
    string Name,
    string FilterText,
    bool IsRegex,
    bool OnlyFlagged,
    string? FilterTimeFrom = null,
    string? FilterTimeTo = null,
    string? ProfileScope = null,
    string? ExcludeText = null,
    bool ExcludeIsRegex = false);

/// <summary>Per-column display state, keyed by column name (SR-10).</summary>
public sealed class ColumnState
{
    public double Width { get; set; }
    public int DisplayIndex { get; set; }
    public bool Visible { get; set; } = true;
}

/// <summary>Persisted sort state for a tab layout (SR-05/SR-10).</summary>
public sealed class SortState
{
    public string? Column { get; set; }
    public bool Descending { get; set; }
}

/// <summary>
/// All locally persisted user settings (SR-10). Stored outside the workspace,
/// under the application's local user-data location (SR-02).
/// </summary>
public sealed class AppSettings
{
    public double WindowWidth { get; set; } = 1150;
    public double WindowHeight { get; set; } = 700;
    public bool WindowMaximized { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WorkspacePanelWidth { get; set; } = 260;

    public List<string> IncludedExtensions { get; set; } = [".log"];

    public bool StreamFollowByDefault { get; set; }

    // Indicator visibility preferences (UR-11) — three independently configurable locations
    public bool ShowIndicatorsInTree { get; set; } = true;
    public bool ShowIndicatorsInTabs { get; set; } = true;
    public bool ShowIndicatorsInSummary { get; set; } = true;

    public List<FilterPreset> FilterPresets { get; set; } = [];

    // Configurable visual rules (UR-10) and flag rules (UR-11), seeded with sensible defaults.
    public List<ColorRuleDto> ColorRules { get; set; } = DefaultRuleSets.ColorRules();
    public List<FlagRuleDto> FlagRules { get; set; } = DefaultRuleSets.FlagRules();

    // Workspace-to-profile assignment and per-file override (UR-06)
    public Dictionary<string, string> DirectoryProfileAssignments { get; set; } = new();
    public Dictionary<string, string> FileProfileOverrides { get; set; } = new();

    // Column layout per profile name (SR-10): profile name -> (column name -> state)
    public Dictionary<string, Dictionary<string, ColumnState>> ColumnLayouts { get; set; } = new();

    // Sort state per profile/layout key (SR-05/SR-10)
    public Dictionary<string, SortState> SortStates { get; set; } = new();
}
