using FluentAssertions;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.Core.Tests;

public class DelimiterParserTests
{
    private static DelimiterProfile PipeProfile(params string[] fieldNames) =>
        new(Delimiter: "||", FieldNames: fieldNames);

    [Fact]
    public void Parse_SplitsLineByDelimiter_AndMapsToNamedFields()
    {
        var lines = new[] { new RawLogLine(1, "2024-01-15||ERROR||Auth||Login failed") };
        var profile = PipeProfile("Timestamp", "Level", "Module", "Message");

        var parser = new DelimiterParser(profile);
        var row = parser.Parse(lines).Single();

        row.Fields["Timestamp"].Should().Be("2024-01-15");
        row.Fields["Level"].Should().Be("ERROR");
        row.Fields["Module"].Should().Be("Auth");
        row.Fields["Message"].Should().Be("Login failed");
    }

    [Fact]
    public void Parse_PreservesLineNumber()
    {
        var lines = new[] { new RawLogLine(42, "a||b||c") };
        var profile = PipeProfile("F1", "F2", "F3");

        var parser = new DelimiterParser(profile);
        var row = parser.Parse(lines).Single();

        row.LineNumber.Should().Be(42);
    }

    [Fact]
    public void Parse_FallsBackToRawText_WhenLineDoesNotMatchDelimiter()
    {
        var lines = new[]
        {
            new RawLogLine(1, "2024-01-15||ERROR||Auth||Login failed"),
            new RawLogLine(2, "this line has no delimiter"),
        };
        var profile = PipeProfile("Timestamp", "Level", "Module", "Message");

        var parser = new DelimiterParser(profile);
        var rows = parser.Parse(lines).ToList();

        rows[0].Fields.ContainsKey("Level").Should().BeTrue();
        rows[1].Fields["RawText"].Should().Be("this line has no delimiter");
        rows[1].Fields.ContainsKey("Level").Should().BeFalse();
    }

    [Fact]
    public void Parse_TracksCountsOfParsedAndFallbackLines()
    {
        var lines = new[]
        {
            new RawLogLine(1, "a||b||c"),
            new RawLogLine(2, "no delimiter here"),
            new RawLogLine(3, "x||y||z"),
        };
        var profile = PipeProfile("F1", "F2", "F3");

        var parser = new DelimiterParser(profile);
        _ = parser.Parse(lines).ToList();

        parser.ParsedCount.Should().Be(2);
        parser.FallbackCount.Should().Be(1);
    }

    [Fact]
    public void Parse_HandlesTabDelimiter()
    {
        var lines = new[] { new RawLogLine(1, "col1\tcol2\tcol3") };
        var profile = new DelimiterProfile(Delimiter: "\t", FieldNames: ["A", "B", "C"]);

        var parser = new DelimiterParser(profile);
        var row = parser.Parse(lines).Single();

        row.Fields["A"].Should().Be("col1");
        row.Fields["B"].Should().Be("col2");
        row.Fields["C"].Should().Be("col3");
    }

    [Fact]
    public void Parse_TruncatesExtraFields_WhenLineHasMoreColumnsThanProfile()
    {
        var lines = new[] { new RawLogLine(1, "a||b||c||d||e") };
        var profile = PipeProfile("F1", "F2", "F3");

        var parser = new DelimiterParser(profile);
        var row = parser.Parse(lines).Single();

        row.Fields.Should().HaveCount(3);
        row.Fields["F1"].Should().Be("a");
        row.Fields["F3"].Should().Be("c");
    }

    [Fact]
    public void Parse_FillsMissingFields_WhenLineHasFewerColumnsThanProfile()
    {
        var lines = new[] { new RawLogLine(1, "a||b") };
        var profile = PipeProfile("F1", "F2", "F3", "F4");

        var parser = new DelimiterParser(profile);
        var row = parser.Parse(lines).Single();

        row.Fields["F1"].Should().Be("a");
        row.Fields["F2"].Should().Be("b");
        row.Fields["F3"].Should().Be("");
        row.Fields["F4"].Should().Be("");
    }
}
