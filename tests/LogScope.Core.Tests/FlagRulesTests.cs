using FluentAssertions;
using LogScope.Core.Parsing;
using LogScope.Core.Visualization;

namespace LogScope.Core.Tests;

public class FlagRulesTests
{
    private static ParsedRow Row(int line, string level, string message) =>
        new(line, new Dictionary<string, string> { ["Level"] = level, ["Message"] = message });

    [Fact]
    public void Evaluate_NoRules_ZeroFlagCount()
    {
        var engine = new FlagRuleEngine([]);
        var result = engine.Evaluate([Row(1, "ERROR", "boom")]);

        result.FlaggedCount.Should().Be(0);
        result.FlaggedLineNumbers.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_FieldValueRule_CountsFlagsCorrectly()
    {
        var rule = FlagRule.ForFieldValue("Level", "ERROR");
        var engine = new FlagRuleEngine([rule]);

        var rows = new[]
        {
            Row(1, "INFO",  "ok"),
            Row(2, "ERROR", "fail"),
            Row(3, "WARN",  "warn"),
            Row(4, "ERROR", "fail again"),
        };

        var result = engine.Evaluate(rows);

        result.FlaggedCount.Should().Be(2);
        result.FlaggedLineNumbers.Should().Equal(2, 4);
    }

    [Fact]
    public void Evaluate_MultipleRules_UnionOfMatches()
    {
        var rules = new[]
        {
            FlagRule.ForFieldValue("Level", "ERROR"),
            FlagRule.ForFieldValue("Level", "FATAL"),
        };
        var engine = new FlagRuleEngine(rules);

        var rows = new[]
        {
            Row(1, "INFO",  "ok"),
            Row(2, "ERROR", "fail"),
            Row(3, "FATAL", "fatal"),
        };

        var result = engine.Evaluate(rows);

        result.FlaggedCount.Should().Be(2);
        result.FlaggedLineNumbers.Should().Equal(2, 3);
    }

    [Fact]
    public void Evaluate_RegexRule_MatchesAcrossFields()
    {
        var rule = FlagRule.ForRegex(@"TIMEOUT|ASSERT");
        var engine = new FlagRuleEngine([rule]);

        var rows = new[]
        {
            Row(1, "WARN",  "Connection TIMEOUT detected"),
            Row(2, "ERROR", "ASSERT failed: value mismatch"),
            Row(3, "INFO",  "All clear"),
        };

        var result = engine.Evaluate(rows);

        result.FlaggedCount.Should().Be(2);
        result.FlaggedLineNumbers.Should().Equal(1, 2);
    }

    [Fact]
    public void Evaluate_SameLineMatchedByMultipleRules_CountedOnce()
    {
        var rules = new[]
        {
            FlagRule.ForFieldValue("Level", "ERROR"),
            FlagRule.ForRegex("fail"),
        };
        var engine = new FlagRuleEngine(rules);

        var rows = new[] { Row(1, "ERROR", "fail") };

        var result = engine.Evaluate(rows);

        result.FlaggedCount.Should().Be(1);
        result.FlaggedLineNumbers.Should().Equal(1);
    }
}
