using FluentAssertions;
using LogScope.Core.Filtering;
using LogScope.Core.Parsing;

namespace LogScope.Core.Tests;

public class FilterEngineTests
{
    private static ParsedRow Row(int line, string level, string module, string message) =>
        new(line, new Dictionary<string, string>
        {
            ["Level"] = level,
            ["Module"] = module,
            ["Message"] = message,
        });

    private static readonly ParsedRow[] SampleRows =
    [
        Row(1, "INFO",  "Auth",    "User logged in"),
        Row(2, "ERROR", "Auth",    "Login failed: timeout"),
        Row(3, "WARN",  "Network", "Retry attempt"),
        Row(4, "ERROR", "DB",      "Connection refused"),
        Row(5, "INFO",  "Network", "Request complete"),
    ];

    [Fact]
    public void Apply_NoFilters_ReturnsAllRows()
    {
        var engine = new FilterEngine([]);
        engine.Apply(SampleRows).Should().HaveCount(5);
    }

    [Fact]
    public void Apply_IncludeByFieldValue_ReturnsOnlyMatchingRows()
    {
        var filter = FilterRule.IncludeWhere("Level", "ERROR");
        var engine = new FilterEngine([filter]);

        var result = engine.Apply(SampleRows).ToList();

        result.Should().HaveCount(2);
        result.All(r => r.Fields["Level"] == "ERROR").Should().BeTrue();
    }

    [Fact]
    public void Apply_ExcludeByFieldValue_RemovesMatchingRows()
    {
        var filter = FilterRule.ExcludeWhere("Module", "Auth");
        var engine = new FilterEngine([filter]);

        var result = engine.Apply(SampleRows).ToList();

        result.Should().HaveCount(3);
        result.Any(r => r.Fields["Module"] == "Auth").Should().BeFalse();
    }

    [Fact]
    public void Apply_IncludeByTextContains_IsCaseSensitiveByDefault()
    {
        var filter = FilterRule.IncludeContainingText("timeout", caseSensitive: true);
        var engine = new FilterEngine([filter]);

        var result = engine.Apply(SampleRows).ToList();

        result.Should().HaveCount(1);
        result[0].LineNumber.Should().Be(2);
    }

    [Fact]
    public void Apply_IncludeByTextContains_CaseInsensitiveMatchesBothCases()
    {
        var filter = FilterRule.IncludeContainingText("TIMEOUT", caseSensitive: false);
        var engine = new FilterEngine([filter]);

        var result = engine.Apply(SampleRows).ToList();

        result.Should().HaveCount(1);
        result[0].LineNumber.Should().Be(2);
    }

    [Fact]
    public void Apply_IncludeByRegex_MatchesPattern()
    {
        var filter = FilterRule.IncludeMatchingRegex(@"failed|refused");
        var engine = new FilterEngine([filter]);

        var result = engine.Apply(SampleRows).ToList();

        result.Should().HaveCount(2);
        result.Select(r => r.LineNumber).Should().Equal(2, 4);
    }

    [Fact]
    public void Apply_ExcludeByRegex_RemovesMatchingRows()
    {
        var filter = FilterRule.ExcludeMatchingRegex(@"^INFO$", fieldName: "Level");
        var engine = new FilterEngine([filter]);

        var result = engine.Apply(SampleRows).ToList();

        result.Should().HaveCount(3);
        result.Any(r => r.Fields["Level"] == "INFO").Should().BeFalse();
    }

    [Fact]
    public void Apply_MultipleFilters_AllMustPass()
    {
        var filters = new[]
        {
            FilterRule.IncludeWhere("Level", "ERROR"),
            FilterRule.ExcludeWhere("Module", "DB"),
        };
        var engine = new FilterEngine(filters);

        var result = engine.Apply(SampleRows).ToList();

        result.Should().HaveCount(1);
        result[0].LineNumber.Should().Be(2);
    }

    [Fact]
    public void Apply_FilterOnMissingField_DoesNotCrash_TreatsAsNonMatch()
    {
        var filter = FilterRule.IncludeWhere("NonExistentField", "value");
        var engine = new FilterEngine([filter]);

        var act = () => engine.Apply(SampleRows).ToList();
        act.Should().NotThrow();

        engine.Apply(SampleRows).Should().BeEmpty();
    }
}
