namespace LogScope.Core.Parsing;

public sealed class ParsedRow
{
    public int LineNumber { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }

    public ParsedRow(int lineNumber, IReadOnlyDictionary<string, string> fields)
    {
        LineNumber = lineNumber;
        Fields = fields;
    }
}
