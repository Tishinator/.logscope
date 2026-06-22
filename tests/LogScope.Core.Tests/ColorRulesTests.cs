using FluentAssertions;
using LogScope.Core.Parsing;
using LogScope.Core.Visualization;

namespace LogScope.Core.Tests;

public class ColorRulesTests
{
    private static ParsedRow Row(string level, string module, string message, int line = 1) =>
        new(line, new Dictionary<string, string>
        {
            ["Level"] = level,
            ["Module"] = module,
            ["Message"] = message,
        });

    [Fact]
    public void Evaluate_NoRules_ReturnsNoStyling()
    {
        var engine = new ColorRuleEngine([]);
        var result = engine.Evaluate(Row("INFO", "Auth", "Done"));

        result.RowBackground.Should().BeNull();
        result.FieldOverrides.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_MatchingFieldValueRule_AppliesRowBackground()
    {
        var rule = ColorRule.ForFieldValue("Level", "ERROR", background: "#FF4444");
        var engine = new ColorRuleEngine([rule]);

        var errorRow = engine.Evaluate(Row("ERROR", "Auth", "Failed"));
        var infoRow  = engine.Evaluate(Row("INFO",  "Auth", "Done"));

        errorRow.RowBackground.Should().Be("#FF4444");
        infoRow.RowBackground.Should().BeNull();
    }

    [Fact]
    public void Evaluate_MessageSubstringRule_AppliesFieldHighlight()
    {
        var rule = ColorRule.ForMessageContaining("timeout", fieldHighlight: "#FFFF00");
        var engine = new ColorRuleEngine([rule]);

        var match   = engine.Evaluate(Row("WARN", "Net", "Connection timeout occurred"));
        var noMatch = engine.Evaluate(Row("INFO", "Net", "Request complete"));

        match.FieldOverrides.Should().ContainKey("Message");
        match.FieldOverrides["Message"].Foreground.Should().Be("#FFFF00");
        noMatch.FieldOverrides.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_RegexRule_MatchesPattern()
    {
        var rule = ColorRule.ForRegex(@"FATAL|ASSERT", background: "#8B0000");
        var engine = new ColorRuleEngine([rule]);

        var fatal  = engine.Evaluate(Row("FATAL",  "Core", "Assertion failed"));
        var assert = engine.Evaluate(Row("ASSERT", "Core", "Check failed"));
        var info   = engine.Evaluate(Row("INFO",   "Core", "Running"));

        fatal.RowBackground.Should().Be("#8B0000");
        assert.RowBackground.Should().Be("#8B0000");
        info.RowBackground.Should().BeNull();
    }

    [Fact]
    public void Evaluate_MultipleRules_AllApplySimultaneously()
    {
        var rules = new[]
        {
            ColorRule.ForFieldValue("Level", "ERROR", background: "#FF4444"),
            ColorRule.ForMessageContaining("timeout", fieldHighlight: "#FFFF00"),
        };
        var engine = new ColorRuleEngine(rules);

        // Row matches both: ERROR level AND contains "timeout"
        var result = engine.Evaluate(Row("ERROR", "Net", "Login timeout"));

        result.RowBackground.Should().Be("#FF4444");
        result.FieldOverrides.Should().ContainKey("Message");
    }

    [Fact]
    public void Evaluate_HigherPriorityRuleWins_ForRowBackground()
    {
        // Rule at index 0 wins over rule at index 1 for the same styling target
        var rules = new[]
        {
            ColorRule.ForFieldValue("Level", "ERROR", background: "#FF4444", priority: 1),
            ColorRule.ForFieldValue("Level", "ERROR", background: "#880000", priority: 2),
        };
        var engine = new ColorRuleEngine(rules);

        var result = engine.Evaluate(Row("ERROR", "Auth", "Failed"));

        result.RowBackground.Should().Be("#880000"); // higher priority wins
    }
}
