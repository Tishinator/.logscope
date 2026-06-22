using FluentAssertions;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.Core.Tests;

public class MultilineGroupingTests
{
    // "New event" rule: line starts with a 4-digit year (timestamp prefix)
    private static readonly MultilineRule StartsWithTimestamp =
        new(NewEventPattern: @"^\d{4}-");

    private static ParsedRow MakeRow(int line, string level = "INFO", string msg = "msg") =>
        new(line, new Dictionary<string, string>
        {
            ["Level"] = level,
            ["Message"] = msg,
        });

    [Fact]
    public void Group_SingleMatchingLine_ProducesOneEventWithNoContinuations()
    {
        var rows = new[] { MakeRow(1) };
        var rawLines = new[] { new RawLogLine(1, "2024-01-15 INFO msg") };

        var grouper = new MultilineGrouper(StartsWithTimestamp);
        var events = grouper.Group(rows, rawLines).ToList();

        events.Should().HaveCount(1);
        events[0].PrimaryRow.LineNumber.Should().Be(1);
        events[0].ContinuationLines.Should().BeEmpty();
    }

    [Fact]
    public void Group_ContinuationLinesAttachToPrecedingEvent()
    {
        var rows = new[]
        {
            MakeRow(1),
            new ParsedRow(2, new Dictionary<string, string> { ["RawText"] = "   at Foo.Bar():42" }),
            new ParsedRow(3, new Dictionary<string, string> { ["RawText"] = "   at Baz.Qux():7" }),
            MakeRow(4),
        };
        var rawLines = new[]
        {
            new RawLogLine(1, "2024-01-15 ERROR Auth Boom"),
            new RawLogLine(2, "   at Foo.Bar():42"),
            new RawLogLine(3, "   at Baz.Qux():7"),
            new RawLogLine(4, "2024-01-15 INFO  Core Done"),
        };

        var grouper = new MultilineGrouper(StartsWithTimestamp);
        var events = grouper.Group(rows, rawLines).ToList();

        events.Should().HaveCount(2);
        events[0].ContinuationLines.Should().HaveCount(2);
        events[0].ContinuationLines[0].Text.Should().Be("   at Foo.Bar():42");
        events[0].ContinuationLines[1].Text.Should().Be("   at Baz.Qux():7");
        events[1].ContinuationLines.Should().BeEmpty();
    }

    [Fact]
    public void Group_ContinuationsAtStart_AttachToSyntheticFirstEvent()
    {
        var rows = new[]
        {
            new ParsedRow(1, new Dictionary<string, string> { ["RawText"] = "startup banner line 1" }),
            new ParsedRow(2, new Dictionary<string, string> { ["RawText"] = "startup banner line 2" }),
            MakeRow(3),
        };
        var rawLines = new[]
        {
            new RawLogLine(1, "startup banner line 1"),
            new RawLogLine(2, "startup banner line 2"),
            new RawLogLine(3, "2024-01-15 INFO Core Ready"),
        };

        var grouper = new MultilineGrouper(StartsWithTimestamp);
        var events = grouper.Group(rows, rawLines).ToList();

        events.Should().HaveCount(2);
        events[0].ContinuationLines.Should().HaveCount(1);
        events[0].ContinuationLines[0].Text.Should().Be("startup banner line 2");
        events[1].PrimaryRow.LineNumber.Should().Be(3);
    }

    [Fact]
    public void Group_PreservesLineNumbersOnBothPrimaryAndContinuationLines()
    {
        var rows = new[]
        {
            MakeRow(10),
            new ParsedRow(11, new Dictionary<string, string> { ["RawText"] = "continuation" }),
        };
        var rawLines = new[]
        {
            new RawLogLine(10, "2024-01-15 ERROR Mod Fail"),
            new RawLogLine(11, "continuation"),
        };

        var grouper = new MultilineGrouper(StartsWithTimestamp);
        var events = grouper.Group(rows, rawLines).ToList();

        events[0].PrimaryRow.LineNumber.Should().Be(10);
        events[0].ContinuationLines[0].LineNumber.Should().Be(11);
    }
}
