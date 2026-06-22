using System.Text.RegularExpressions;
using LogScope.Core.Parsing;

namespace LogScope.Core.Filtering;

public sealed class FilterEngine
{
    private readonly IReadOnlyList<FilterRule> _rules;
    private readonly IReadOnlyList<(FilterRule Rule, Regex? Regex)> _compiled;

    public FilterEngine(IEnumerable<FilterRule> rules)
    {
        _rules = rules.ToList();
        _compiled = _rules.Select(r =>
        {
            Regex? regex = r.Kind is FilterRule.RuleKind.IncludeRegex or FilterRule.RuleKind.ExcludeRegex
                ? new Regex(r.Value, RegexOptions.Compiled | (r.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase))
                : null;
            return (r, regex);
        }).ToList();
    }

    public IEnumerable<ParsedRow> Apply(IEnumerable<ParsedRow> rows)
    {
        foreach (var row in rows)
        {
            if (Passes(row))
                yield return row;
        }
    }

    private bool Passes(ParsedRow row)
    {
        foreach (var (rule, regex) in _compiled)
        {
            if (!Evaluate(rule, regex, row))
                return false;
        }
        return true;
    }

    private static bool Evaluate(FilterRule rule, Regex? regex, ParsedRow row)
    {
        var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        switch (rule.Kind)
        {
            case FilterRule.RuleKind.IncludeFieldValue:
                return row.Fields.TryGetValue(rule.FieldName!, out var fv) && fv == rule.Value;

            case FilterRule.RuleKind.ExcludeFieldValue:
                return !row.Fields.TryGetValue(rule.FieldName!, out var ev) || ev != rule.Value;

            case FilterRule.RuleKind.IncludeText:
                return row.Fields.Values.Any(v => v.Contains(rule.Value, comparison));

            case FilterRule.RuleKind.IncludeRegex:
                var searchValues = rule.FieldName != null
                    ? row.Fields.TryGetValue(rule.FieldName, out var fvr) ? [fvr] : Array.Empty<string>()
                    : row.Fields.Values.ToArray();
                return searchValues.Any(v => regex!.IsMatch(v));

            case FilterRule.RuleKind.ExcludeRegex:
                var excludeValues = rule.FieldName != null
                    ? row.Fields.TryGetValue(rule.FieldName, out var fve) ? [fve] : Array.Empty<string>()
                    : row.Fields.Values.ToArray();
                return !excludeValues.Any(v => regex!.IsMatch(v));

            default:
                return true;
        }
    }
}
