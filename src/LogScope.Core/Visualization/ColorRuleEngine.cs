using System.Text.RegularExpressions;
using LogScope.Core.Parsing;

namespace LogScope.Core.Visualization;

public sealed class ColorRuleEngine
{
    private readonly IReadOnlyList<(ColorRule Rule, Regex? Regex)> _rules;

    public ColorRuleEngine(IEnumerable<ColorRule> rules)
    {
        _rules = rules
            .OrderBy(r => r.Priority)
            .Select(r =>
            {
                Regex? regex = r.Kind == ColorRule.MatchKind.Regex
                    ? new Regex(r.MatchValue, RegexOptions.Compiled | RegexOptions.IgnoreCase)
                    : null;
                return (r, regex);
            })
            .ToList();
    }

    public RowStyling Evaluate(ParsedRow row)
    {
        string? rowBackground = null;
        var fieldOverrides = new Dictionary<string, FieldStyling>();

        foreach (var (rule, regex) in _rules)
        {
            if (!Matches(rule, regex, row))
                continue;

            if (rule.Background != null)
                rowBackground = rule.Background;

            if (rule.FieldHighlight != null)
            {
                var fieldName = rule.FieldName ?? "Message";
                fieldOverrides[fieldName] = new FieldStyling(foreground: rule.FieldHighlight);
            }
        }

        return new RowStyling(rowBackground, fieldOverrides);
    }

    private static bool Matches(ColorRule rule, Regex? regex, ParsedRow row)
    {
        return rule.Kind switch
        {
            ColorRule.MatchKind.FieldValue =>
                row.Fields.TryGetValue(rule.FieldName!, out var fv) && fv == rule.MatchValue,

            ColorRule.MatchKind.MessageContaining =>
                row.Fields.TryGetValue("Message", out var msg) &&
                msg.Contains(rule.MatchValue, StringComparison.OrdinalIgnoreCase),

            ColorRule.MatchKind.Regex =>
                row.Fields.Values.Any(v => regex!.IsMatch(v)),

            _ => false
        };
    }
}
