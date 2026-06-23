using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.App.ViewModels;

/// <summary>
/// A single display row in the table view. Wraps a parsed row plus any multiline
/// continuation lines and the computed row background from color rules.
/// </summary>
public sealed class LogRowViewModel
{
    private readonly ParsedRow _row;

    public int LineNumber => _row.LineNumber;
    public IReadOnlyDictionary<string, string> Fields => _row.Fields;
    public string RawText => _row.Fields.TryGetValue("RawText", out var t)
        ? t
        : string.Join(" ", _row.Fields.Values);

    public IReadOnlyList<RawLogLine> ContinuationLines { get; }
    public bool HasContinuation => ContinuationLines.Count > 0;
    public string ContinuationText => string.Join(Environment.NewLine, ContinuationLines.Select(l => l.Text));

    public string? RowBackground { get; }
    public bool IsFlagged { get; }

    public LogRowViewModel(
        ParsedRow row,
        IReadOnlyList<RawLogLine> continuationLines,
        string? rowBackground,
        bool isFlagged)
    {
        _row = row;
        ContinuationLines = continuationLines;
        RowBackground = rowBackground;
        IsFlagged = isFlagged;
    }

    public string GetField(string column) =>
        _row.Fields.TryGetValue(column, out var v) ? v : string.Empty;
}
