namespace LogScope.Core.Visualization;

public sealed class FieldStyling
{
    public string? Foreground { get; }
    public string? Background { get; }

    public FieldStyling(string? foreground = null, string? background = null)
    {
        Foreground = foreground;
        Background = background;
    }
}

public sealed class RowStyling
{
    public string? RowBackground { get; }
    public IReadOnlyDictionary<string, FieldStyling> FieldOverrides { get; }

    public RowStyling(string? rowBackground, IReadOnlyDictionary<string, FieldStyling> fieldOverrides)
    {
        RowBackground = rowBackground;
        FieldOverrides = fieldOverrides;
    }
}
