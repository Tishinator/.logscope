using FluentAssertions;
using LogScope.Core.Parsing;
using LogScope.Core.Search;

namespace LogScope.Core.Tests;

public class SearchEngineTests
{
    private static ParsedRow Row(int line, string level, string message) =>
        new(line, new Dictionary<string, string> { ["Level"] = level, ["Message"] = message });

    private static readonly ParsedRow[] SampleRows =
    [
        Row(1,  "INFO",  "User logged in"),
        Row(2,  "ERROR", "Login timeout occurred"),
        Row(3,  "WARN",  "Retry scheduled"),
        Row(4,  "ERROR", "Timeout threshold exceeded"),
        Row(5,  "INFO",  "Request complete"),
    ];

    [Fact]
    public void Search_TextMatch_ReturnsMatchingLineNumbers()
    {
        var engine = new SearchEngine();
        var results = engine.Search(SampleRows, new SearchQuery(Text: "Timeout", CaseSensitive: false));

        results.Select(r => r.LineNumber).Should().Equal(2, 4);
    }

    [Fact]
    public void Search_CaseSensitive_DoesNotMatchDifferentCase()
    {
        var engine = new SearchEngine();
        var results = engine.Search(SampleRows, new SearchQuery(Text: "timeout", CaseSensitive: true));

        results.Select(r => r.LineNumber).Should().Equal(2);
    }

    [Fact]
    public void Search_RegexPattern_MatchesPattern()
    {
        var engine = new SearchEngine();
        var results = engine.Search(SampleRows, new SearchQuery(Text: @"time\w+", CaseSensitive: false, IsRegex: true));

        results.Select(r => r.LineNumber).Should().Equal(2, 4);
    }

    [Fact]
    public void Search_WholeWord_OnlyMatchesWholeWords()
    {
        var engine = new SearchEngine();
        // "logged" is a whole word; "log" is not (it is inside "logged")
        var wholeWord = engine.Search(SampleRows, new SearchQuery(Text: "log", WholeWord: true, CaseSensitive: false));
        var partial   = engine.Search(SampleRows, new SearchQuery(Text: "log", WholeWord: false, CaseSensitive: false));

        wholeWord.Should().BeEmpty();
        partial.Should().HaveCount(2); // "logged" (row 1) and "Login" (row 2) both contain "log" case-insensitively
    }

    [Fact]
    public void Search_ColumnScoped_OnlySearchesSpecifiedField()
    {
        var engine = new SearchEngine();
        // "ERROR" appears in Level column, not in Message
        var scoped    = engine.Search(SampleRows, new SearchQuery(Text: "ERROR", FieldName: "Level",   CaseSensitive: true));
        var unscoped  = engine.Search(SampleRows, new SearchQuery(Text: "ERROR", FieldName: "Message", CaseSensitive: true));

        scoped.Should().HaveCount(2);
        unscoped.Should().BeEmpty();
    }

    [Fact]
    public void Search_ReturnsMatchReference_WithLineNumberAndFieldAndOffset()
    {
        var engine = new SearchEngine();
        var results = engine.Search(SampleRows, new SearchQuery(Text: "timeout", CaseSensitive: false));

        var first = results.First();
        first.LineNumber.Should().Be(2);
        first.FieldName.Should().Be("Message");
        first.MatchIndex.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsNoResults()
    {
        var engine = new SearchEngine();
        var results = engine.Search(SampleRows, new SearchQuery(Text: "", CaseSensitive: false));

        results.Should().BeEmpty();
    }
}
