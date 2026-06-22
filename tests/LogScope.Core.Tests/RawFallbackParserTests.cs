using FluentAssertions;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.Core.Tests;

public class RawFallbackParserTests
{
    [Fact]
    public void Parse_ProducesOneRowPerLine_WithLineNumberAndRawText()
    {
        var lines = new[]
        {
            new RawLogLine(1, "first line"),
            new RawLogLine(2, "second line"),
            new RawLogLine(3, "third line"),
        };

        var parser = new RawFallbackParser();
        var rows = parser.Parse(lines).ToList();

        rows.Should().HaveCount(3);
        rows[0].LineNumber.Should().Be(1);
        rows[0].Fields["RawText"].Should().Be("first line");
        rows[1].LineNumber.Should().Be(2);
        rows[1].Fields["RawText"].Should().Be("second line");
    }

    [Fact]
    public void Parse_EmitsRawTextFieldForEveryLine_IncludingBlankLines()
    {
        var lines = new[]
        {
            new RawLogLine(1, "something"),
            new RawLogLine(2, ""),
            new RawLogLine(3, "after blank"),
        };

        var parser = new RawFallbackParser();
        var rows = parser.Parse(lines).ToList();

        rows.Should().HaveCount(3);
        rows[1].Fields["RawText"].Should().Be("");
    }

    [Fact]
    public void Parse_PreservesPhysicalLineNumber_EvenForBlankLines()
    {
        var lines = Enumerable.Range(1, 5)
            .Select(i => new RawLogLine(i, $"line {i}"))
            .ToArray();

        var parser = new RawFallbackParser();
        var rows = parser.Parse(lines).ToList();

        rows.Select(r => r.LineNumber).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Parse_ReportsFullParseSuccess_BecauseEveryLineIsAlwaysHandled()
    {
        var lines = new[] { new RawLogLine(1, "anything") };

        var parser = new RawFallbackParser();
        _ = parser.Parse(lines).ToList();

        parser.ParsedCount.Should().Be(1);
        parser.FallbackCount.Should().Be(0);
    }

    [Fact]
    public void ParsedRow_HasExactlyTwoFields_LineNumberImplicitAndRawText()
    {
        var lines = new[] { new RawLogLine(7, "hello") };

        var parser = new RawFallbackParser();
        var row = parser.Parse(lines).Single();

        row.Fields.Keys.Should().ContainSingle().Which.Should().Be("RawText");
        row.LineNumber.Should().Be(7);
    }
}
