using LogScope.Core.Visualization;

namespace LogScope.App.ViewModels;

/// <summary>
/// Sensible out-of-the-box color and flag rules so the app is useful immediately.
/// Users can extend these later; for the MVP they ship as defaults.
/// </summary>
public static class DefaultRules
{
    public static IReadOnlyList<ColorRule> ColorRules { get; } =
    [
        ColorRule.ForRegex(@"\b(ERROR|FATAL|FAIL|EXCEPTION|ASSERT)\b", background: "#5A1F1F", priority: 10),
        ColorRule.ForRegex(@"\b(WARN|WARNING|TIMEOUT)\b", background: "#5A4A1F", priority: 5),
    ];

    public static IReadOnlyList<FlagRule> FlagRules { get; } =
    [
        FlagRule.ForRegex(@"\b(ERROR|FATAL|FAIL|ASSERT|TIMEOUT|EXCEPTION|WARN)\b"),
    ];
}
