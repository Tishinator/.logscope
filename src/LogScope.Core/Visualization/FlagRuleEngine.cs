using System.Text.RegularExpressions;
using LogScope.Core.Parsing;

namespace LogScope.Core.Visualization;

public sealed class FlagRuleEngine
{
    private readonly IReadOnlyList<(FlagRule Rule, Regex? Regex)> _rules;

    public FlagRuleEngine(IEnumerable<FlagRule> rules)
    {
        _rules = rules.Select(r =>
        {
            Regex? regex = r.Kind == FlagRule.MatchKind.Regex
                ? new Regex(r.MatchValue, RegexOptions.Compiled | RegexOptions.IgnoreCase)
                : null;
            return (r, regex);
        }).ToList();
    }

    public FlagResult Evaluate(IEnumerable<ParsedRow> rows)
    {
        var flagged = new SortedSet<int>();

        foreach (var row in rows)
        {
            foreach (var (rule, regex) in _rules)
            {
                if (Matches(rule, regex, row))
                {
                    flagged.Add(row.LineNumber);
                    break;
                }
            }
        }

        return new FlagResult(flagged.ToList());
    }

    private static bool Matches(FlagRule rule, Regex? regex, ParsedRow row)
    {
        return rule.Kind switch
        {
            FlagRule.MatchKind.FieldValue =>
                row.Fields.TryGetValue(rule.FieldName!, out var fv) && fv == rule.MatchValue,

            FlagRule.MatchKind.Contains =>
                TargetValues(rule, row).Any(v => v.Contains(rule.MatchValue, StringComparison.OrdinalIgnoreCase)),

            FlagRule.MatchKind.Regex =>
                TargetValues(rule, row).Any(v => regex!.IsMatch(v)),

            _ => false
        };
    }

    /// <summary>The field values a Contains/Regex rule tests: one named field, or all fields.</summary>
    private static IEnumerable<string> TargetValues(FlagRule rule, ParsedRow row) =>
        rule.FieldName != null
            ? (row.Fields.TryGetValue(rule.FieldName, out var v) ? [v] : Array.Empty<string>())
            : row.Fields.Values;
}
