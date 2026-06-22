using System.Text.RegularExpressions;
using LogScope.Core.Parsing;

namespace LogScope.Core.Search;

public sealed class SearchEngine
{
    public IEnumerable<SearchMatch> Search(IEnumerable<ParsedRow> rows, SearchQuery query)
    {
        if (string.IsNullOrEmpty(query.Text))
            yield break;

        var regex = BuildRegex(query);

        foreach (var row in rows)
        {
            var fieldsToSearch = query.FieldName != null
                ? row.Fields.Where(f => f.Key == query.FieldName)
                : row.Fields;

            foreach (var (fieldName, value) in fieldsToSearch)
            {
                var match = regex.Match(value);
                if (match.Success)
                    yield return new SearchMatch(row.LineNumber, fieldName, match.Index);
            }
        }
    }

    private static Regex BuildRegex(SearchQuery query)
    {
        var options = RegexOptions.None;
        if (!query.CaseSensitive)
            options |= RegexOptions.IgnoreCase;

        string pattern = query.IsRegex
            ? query.Text
            : Regex.Escape(query.Text);

        if (query.WholeWord)
            pattern = $@"\b{pattern}\b";

        return new Regex(pattern, options | RegexOptions.Compiled);
    }
}
