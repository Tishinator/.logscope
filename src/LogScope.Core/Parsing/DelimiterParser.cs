using LogScope.Core.Reading;

namespace LogScope.Core.Parsing;

public sealed class DelimiterParser
{
    private readonly DelimiterProfile _profile;

    public int ParsedCount { get; private set; }
    public int FallbackCount { get; private set; }

    public DelimiterParser(DelimiterProfile profile) => _profile = profile;

    public IEnumerable<ParsedRow> Parse(IEnumerable<RawLogLine> lines)
    {
        ParsedCount = 0;
        FallbackCount = 0;

        foreach (var line in lines)
        {
            var parts = line.Text.Split(_profile.Delimiter, StringSplitOptions.None);

            if (parts.Length < 2 && !line.Text.Contains(_profile.Delimiter))
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
            for (int i = 0; i < _profile.FieldNames.Length; i++)
            {
                fields[_profile.FieldNames[i]] = i < parts.Length ? parts[i] : "";
            }
            yield return new ParsedRow(line.LineNumber, fields);
        }
    }
}
