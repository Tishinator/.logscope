using System.Text;
using LogScope.Core.Parsing;

namespace LogScope.Core.Documents;

/// <summary>
/// Formats selected rows for the clipboard (UR-14): tab-separated table, raw text, or
/// line references. The UI is responsible for actually placing the result on the clipboard.
/// </summary>
public static class ClipboardFormatter
{
    public static string RowsAsTsv(IReadOnlyList<ParsedRow> rows, IReadOnlyList<string> columns)
    {
        if (rows.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("Line");
        foreach (var col in columns)
            sb.Append('\t').Append(col);
        sb.Append('\n');

        for (int r = 0; r < rows.Count; r++)
        {
            sb.Append(rows[r].LineNumber);
            foreach (var col in columns)
                sb.Append('\t').Append(rows[r].Fields.TryGetValue(col, out var v) ? v : string.Empty);
            if (r < rows.Count - 1)
                sb.Append('\n');
        }
        return sb.ToString();
    }

    public static string RawText(IReadOnlyList<ParsedRow> rows) =>
        string.Join("\n", rows.Select(r =>
            r.Fields.TryGetValue("RawText", out var t) ? t : string.Join(" ", r.Fields.Values)));

    public static string LineReferences(IReadOnlyList<ParsedRow> rows) =>
        string.Join(", ", rows.Select(r => r.LineNumber));
}
