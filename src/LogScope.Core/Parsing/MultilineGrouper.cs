using System.Text.RegularExpressions;
using LogScope.Core.Reading;

namespace LogScope.Core.Parsing;

public sealed class MultilineGrouper
{
    private readonly Regex _newEventRegex;

    public MultilineGrouper(MultilineRule rule)
    {
        _newEventRegex = new Regex(rule.NewEventPattern, RegexOptions.Compiled);
    }

    public IEnumerable<LogEvent> Group(IEnumerable<ParsedRow> rows, IEnumerable<RawLogLine> rawLines)
    {
        var rowList = rows.ToList();
        var rawList = rawLines.ToList();

        if (rowList.Count == 0)
            yield break;

        var rawByLine = rawList.ToDictionary(r => r.LineNumber);

        ParsedRow? currentEvent = null;
        var continuations = new List<RawLogLine>();

        foreach (var row in rowList)
        {
            var rawLine = rawByLine.GetValueOrDefault(row.LineNumber);
            bool isNewEvent = rawLine != null && _newEventRegex.IsMatch(rawLine.Text);

            if (currentEvent == null)
            {
                currentEvent = row;
                continue;
            }

            if (isNewEvent)
            {
                yield return new LogEvent(currentEvent, continuations.ToList());
                continuations.Clear();
                currentEvent = row;
            }
            else
            {
                if (rawLine != null)
                    continuations.Add(rawLine);
            }
        }

        if (currentEvent != null)
            yield return new LogEvent(currentEvent, continuations.ToList());
    }
}
