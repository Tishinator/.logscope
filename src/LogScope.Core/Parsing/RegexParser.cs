using System.Text.RegularExpressions;
using LogScope.Core.Reading;

namespace LogScope.Core.Parsing;

public sealed class RegexParser
{
    private readonly Regex _regex;

    public int ParsedCount { get; private set; }
    public int FallbackCount { get; private set; }

    public RegexParser(RegexProfile profile)
    {
        try
        {
            _regex = new Regex(profile.Pattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidParserProfileException($"Invalid regex pattern: {profile.Pattern}", ex);
        }
    }

    public IEnumerable<ParsedRow> Parse(IEnumerable<RawLogLine> lines)
    {
        ParsedCount = 0;
        FallbackCount = 0;

        foreach (var line in lines)
        {
            var match = _regex.Match(line.Text);
            if (!match.Success)
            {
                FallbackCount++;
                yield return new ParsedRow(line.LineNumber, new Dictionary<string, string>
                {
                    ["RawText"] = line.Text
                });
                continue;
            }

            ParsedCount++;
            var fields = new Dictionary<string, string>();
            foreach (Group group in match.Groups)
            {
                if (group.Name != "0")
                    fields[group.Name] = group.Value;
            }
            yield return new ParsedRow(line.LineNumber, fields);
        }
    }
}
