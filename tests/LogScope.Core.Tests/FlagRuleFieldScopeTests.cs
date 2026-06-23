using FluentAssertions;
using LogScope.Core.Parsing;
using LogScope.Core.Visualization;

namespace LogScope.Core.Tests;

public class FlagRuleFieldScopeTests
{
    private static ParsedRow Row(int line, string level, string message) =>
        new(line, new Dictionary<string, string> { ["Level"] = level, ["Message"] = message });

    private static readonly ParsedRow[] Rows =
    [
        Row(1, "INFO",  "user ERROR-code lookup ok"), // "ERROR" appears in Message, not Level
        Row(2, "ERROR", "boom"),
    ];

    [Fact]
    public void Regex_ScopedToField_OnlyMatchesThatField()
    {
        var engine = new FlagRuleEngine([FlagRule.ForRegex("ERROR", fieldName: "Level")]);

        var result = engine.Evaluate(Rows);

        // Only line 2 has ERROR in the Level field; line 1 has it only in Message.
        result.FlaggedLineNumbers.Should().Equal(2);
    }

    [Fact]
    public void Regex_AllFields_MatchesAnyField()
    {
        var engine = new FlagRuleEngine([FlagRule.ForRegex("ERROR")]);

        var result = engine.Evaluate(Rows);

        result.FlaggedLineNumbers.Should().Equal(1, 2);
    }

    [Fact]
    public void Contains_ScopedToField_IsCaseInsensitive()
    {
        var engine = new FlagRuleEngine([FlagRule.ForContains("boom", fieldName: "Message")]);

        engine.Evaluate(Rows).FlaggedLineNumbers.Should().Equal(2);
    }

    [Fact]
    public void Contains_AllFields_WhenNoFieldGiven()
    {
        var engine = new FlagRuleEngine([FlagRule.ForContains("INFO")]);

        engine.Evaluate(Rows).FlaggedLineNumbers.Should().Equal(1);
    }
}
