using FluentAssertions;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.Core.Tests;

public class RegexParserTests
{
    private static readonly string StandardPattern =
        @"^(?<Timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\s+(?<Level>\w+)\s+(?<Module>\w+)\s+(?<Message>.+)$";

    [Fact]
    public void Parse_ExtractsNamedGroups_AsFields()
    {
        var lines = new[] { new RawLogLine(1, "2024-01-15 10:23:45 ERROR Auth Login failed") };
        var profile = new RegexProfile(Pattern: StandardPattern);

        var parser = new RegexParser(profile);
        var row = parser.Parse(lines).Single();

        row.Fields["Timestamp"].Should().Be("2024-01-15 10:23:45");
        row.Fields["Level"].Should().Be("ERROR");
        row.Fields["Module"].Should().Be("Auth");
        row.Fields["Message"].Should().Be("Login failed");
    }

    [Fact]
    public void Parse_PreservesLineNumber()
    {
        var lines = new[] { new RawLogLine(99, "2024-01-15 10:23:45 ERROR Auth Login failed") };
        var profile = new RegexProfile(Pattern: StandardPattern);

        var parser = new RegexParser(profile);
        var row = parser.Parse(lines).Single();

        row.LineNumber.Should().Be(99);
    }

    [Fact]
    public void Parse_FallsBackToRawText_WhenLineDoesNotMatch()
    {
        var lines = new[]
        {
            new RawLogLine(1, "2024-01-15 10:23:45 ERROR Auth Login failed"),
            new RawLogLine(2, "   at com.example.Foo.bar(Foo.java:42)"),
        };
        var profile = new RegexProfile(Pattern: StandardPattern);

        var parser = new RegexParser(profile);
        var rows = parser.Parse(lines).ToList();

        rows[0].Fields.ContainsKey("Level").Should().BeTrue();
        rows[1].Fields["RawText"].Should().Be("   at com.example.Foo.bar(Foo.java:42)");
    }

    [Fact]
    public void Parse_TracksCountsOfParsedAndFallbackLines()
    {
        var lines = new[]
        {
            new RawLogLine(1, "2024-01-15 10:23:45 ERROR Auth Msg"),
            new RawLogLine(2, "not a match"),
            new RawLogLine(3, "2024-01-15 10:23:46 INFO  Core Done"),
        };
        var profile = new RegexProfile(Pattern: StandardPattern);

        var parser = new RegexParser(profile);
        _ = parser.Parse(lines).ToList();

        parser.ParsedCount.Should().Be(2);
        parser.FallbackCount.Should().Be(1);
    }

    [Fact]
    public void RegexParser_ThrowsOnConstruction_WhenPatternIsInvalid()
    {
        var act = () => new RegexParser(new RegexProfile(Pattern: "[invalid("));
        act.Should().Throw<InvalidParserProfileException>();
    }
}
