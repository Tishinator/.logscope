namespace LogScope.Core.Persistence;

/// <summary>A saved filter preset (UR-08).</summary>
public sealed record FilterPreset(string Name, string FilterText, bool IsRegex, bool OnlyFlagged);

/// <summary>Per-column display state, keyed by column name (SR-10).</summary>
public sealed class ColumnState
{
    public double Width { get; set; }
    public int DisplayIndex { get; set; }
    public bool Visible { get; set; } = true;
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

    public List<string> IncludedExtensions { get; set; } = [".log"];

    public bool StreamFollowByDefault { get; set; }

    // Indicator visibility preferences (UR-11)
    public bool ShowIndicatorsInTree { get; set; } = true;
    public bool ShowIndicatorsInSummary { get; set; } = true;

    public List<FilterPreset> FilterPresets { get; set; } = [];

    // Workspace-to-profile assignment and per-file override (UR-06)
    public Dictionary<string, string> DirectoryProfileAssignments { get; set; } = new();
    public Dictionary<string, string> FileProfileOverrides { get; set; } = new();

    // Column layout per profile name (SR-10): profile name -> (column name -> state)
    public Dictionary<string, Dictionary<string, ColumnState>> ColumnLayouts { get; set; } = new();
}
