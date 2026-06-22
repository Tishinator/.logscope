using LogScope.Core.Reading;

namespace LogScope.Core.Parsing;

public sealed class LogEvent
{
    public ParsedRow PrimaryRow { get; }
    public IReadOnlyList<RawLogLine> ContinuationLines { get; }

    public LogEvent(ParsedRow primaryRow, IReadOnlyList<RawLogLine> continuationLines)
    {
        PrimaryRow = primaryRow;
        ContinuationLines = continuationLines;
    }
}
