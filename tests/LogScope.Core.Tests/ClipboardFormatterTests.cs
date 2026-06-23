using FluentAssertions;
using LogScope.Core.Documents;
using LogScope.Core.Parsing;

namespace LogScope.Core.Tests;

public class ClipboardFormatterTests
{
    private static ParsedRow Row(int line, string level, string message) =>
        new(line, new Dictionary<string, string> { ["Level"] = level, ["Message"] = message });

    private static readonly string[] Columns = ["Level", "Message"];

    private static readonly ParsedRow[] Rows =
    [
        Row(1, "INFO", "started"),
        Row(2, "ERROR", "boom"),
    ];

    [Fact]
    public void RowsAsTsv_IncludesHeaderWithLineColumn()
    {
        var tsv = ClipboardFormatter.RowsAsTsv(Rows, Columns);
        var lines = tsv.Split('\n');

        lines[0].Should().Be("Line\tLevel\tMessage");
        lines[1].Should().Be("1\tINFO\tstarted");
        lines[2].Should().Be("2\tERROR\tboom");
    }

    [Fact]
    public void RowsAsTsv_FillsEmptyForMissingFields()
    {
        var rows = new[] { new ParsedRow(9, new Dictionary<string, string> { ["Level"] = "WARN" }) };
        var tsv = ClipboardFormatter.RowsAsTsv(rows, Columns);

        tsv.Split('\n')[1].Should().Be("9\tWARN\t");
    }

    [Fact]
    public void RawText_JoinsRawTextValues()
    {
        var rows = new[]
        {
            new ParsedRow(1, new Dictionary<string, string> { ["RawText"] = "first" }),
            new ParsedRow(2, new Dictionary<string, string> { ["RawText"] = "second" }),
        };

        ClipboardFormatter.RawText(rows).Should().Be("first" + "\n" + "second");
    }

    [Fact]
    public void RawText_FallsBackToJoinedFields_WhenNoRawText()
    {
        ClipboardFormatter.RawText([Row(1, "INFO", "hello")]).Should().Contain("INFO").And.Contain("hello");
    }

    [Fact]
    public void LineReferences_FormatsLineNumbers()
    {
        ClipboardFormatter.LineReferences(Rows).Should().Be("1, 2");
    }

    [Fact]
    public void RowsAsTsv_EmptySelection_ReturnsEmptyString()
    {
        ClipboardFormatter.RowsAsTsv([], Columns).Should().BeEmpty();
    }
}
