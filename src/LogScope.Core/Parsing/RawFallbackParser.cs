using LogScope.Core.Reading;

namespace LogScope.Core.Parsing;

public sealed class RawFallbackParser
{
    public int ParsedCount { get; private set; }
    public int FallbackCount { get; private set; }

    public IEnumerable<ParsedRow> Parse(IEnumerable<RawLogLine> lines)
    {
        ParsedCount = 0;
        FallbackCount = 0;

        foreach (var line in lines)
        {
            ParsedCount++;
            yield return new ParsedRow(line.LineNumber, new Dictionary<string, string>
            {
                ["RawText"] = line.Text
            });
        }
    }
}
