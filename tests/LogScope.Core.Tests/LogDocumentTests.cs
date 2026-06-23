using FluentAssertions;
using LogScope.Core.Documents;
using LogScope.Core.Parsing;

namespace LogScope.Core.Tests;

public class LogDocumentTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose() => File.Delete(_tempFile);

    private void Write(params string[] lines) => File.WriteAllLines(_tempFile, lines);

    [Fact]
    public void Load_WithRawProfile_ExposesEveryLineAsRow()
    {
        Write("line one", "line two", "line three");

        var doc = LogDocument.Load(_tempFile, LogProfile.Raw());

        doc.Rows.Should().HaveCount(3);
        doc.Rows[0].Fields["RawText"].Should().Be("line one");
    }

    [Fact]
    public void Load_PreservesRawLines_Separately()
    {
        Write("alpha", "beta");

        var doc = LogDocument.Load(_tempFile, LogProfile.Raw());

        doc.RawLines.Should().HaveCount(2);
        doc.RawLines[0].Text.Should().Be("alpha");
        doc.RawLines[1].LineNumber.Should().Be(2);
    }

    [Fact]
    public void Load_WithDelimiterProfile_ParsesNamedColumns()
    {
        Write("2024-01-15|ERROR|Auth|Failed", "2024-01-15|INFO|Core|OK");

        var profile = LogProfile.Delimited("|", ["Timestamp", "Level", "Module", "Message"]);
        var doc = LogDocument.Load(_tempFile, profile);

        doc.Rows[0].Fields["Level"].Should().Be("ERROR");
        doc.Columns.Should().ContainInOrder("Timestamp", "Level", "Module", "Message");
    }

    [Fact]
    public void Load_WithRegexProfile_ParsesNamedGroups()
    {
        Write("2024-01-15 ERROR Login failed");

        var profile = LogProfile.Regex(@"^(?<Date>\S+)\s+(?<Level>\w+)\s+(?<Message>.+)$");
        var doc = LogDocument.Load(_tempFile, profile);

        doc.Rows[0].Fields["Level"].Should().Be("ERROR");
        doc.Rows[0].Fields["Message"].Should().Be("Login failed");
    }

    [Fact]
    public void Load_ReportsParseStatistics()
    {
        Write("a|b|c", "no delimiter line", "d|e|f");

        var profile = LogProfile.Delimited("|", ["F1", "F2", "F3"]);
        var doc = LogDocument.Load(_tempFile, profile);

        doc.ParsedCount.Should().Be(2);
        doc.FallbackCount.Should().Be(1);
    }

    [Fact]
    public void Load_WithMultilineRule_GroupsContinuationLines()
    {
        Write(
            "2024-01-15 ERROR Boom",
            "   at Foo.Bar()",
            "   at Baz.Qux()",
            "2024-01-15 INFO Done");

        var profile = LogProfile.Regex(@"^(?<Date>\d{4}-\d{2}-\d{2})\s+(?<Level>\w+)\s+(?<Message>.+)$")
            .WithMultiline(@"^\d{4}-");
        var doc = LogDocument.Load(_tempFile, profile);

        doc.Events.Should().HaveCount(2);
        doc.Events[0].ContinuationLines.Should().HaveCount(2);
    }

    [Fact]
    public void Load_WithoutMultilineRule_EachRowIsOwnEvent()
    {
        Write("a|b", "c|d");

        var profile = LogProfile.Delimited("|", ["F1", "F2"]);
        var doc = LogDocument.Load(_tempFile, profile);

        doc.Events.Should().HaveCount(2);
        doc.Events.Should().OnlyContain(e => e.ContinuationLines.Count == 0);
    }

    [Fact]
    public void Load_RawProfile_ColumnsContainLineNumberAndRawText()
    {
        Write("anything");

        var doc = LogDocument.Load(_tempFile, LogProfile.Raw());

        doc.Columns.Should().Contain("RawText");
    }

    [Fact]
    public void FilePath_IsExposed()
    {
        Write("x");
        var doc = LogDocument.Load(_tempFile, LogProfile.Raw());
        doc.FilePath.Should().Be(_tempFile);
    }
}
