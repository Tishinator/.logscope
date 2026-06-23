using FluentAssertions;
using LogScope.Core.Persistence;
using LogScope.Core.Visualization;

namespace LogScope.Core.Tests;

public class RuleDtoTests
{
    [Fact]
    public void ColorRuleDto_RoundTrips_FieldValueRule()
    {
        var rule = ColorRule.ForFieldValue("Level", "ERROR", background: "#FF0000", priority: 7);

        var restored = ColorRuleDto.From(rule).ToRule();

        restored.Kind.Should().Be(ColorRule.MatchKind.FieldValue);
        restored.FieldName.Should().Be("Level");
        restored.MatchValue.Should().Be("ERROR");
        restored.Background.Should().Be("#FF0000");
        restored.Priority.Should().Be(7);
    }

    [Fact]
    public void ColorRuleDto_RoundTrips_RegexRule()
    {
        var rule = ColorRule.ForRegex(@"TIMEOUT", background: "#5A4A1F", priority: 3);

        var restored = ColorRuleDto.From(rule).ToRule();

        restored.Kind.Should().Be(ColorRule.MatchKind.Regex);
        restored.MatchValue.Should().Be("TIMEOUT");
        restored.Background.Should().Be("#5A4A1F");
    }

    [Fact]
    public void ColorRuleDto_RoundTrips_MessageContainingWithFieldHighlight()
    {
        var rule = ColorRule.ForMessageContaining("timeout", fieldHighlight: "#FFFF00", priority: 1);

        var restored = ColorRuleDto.From(rule).ToRule();

        restored.Kind.Should().Be(ColorRule.MatchKind.MessageContaining);
        restored.FieldHighlight.Should().Be("#FFFF00");
    }

    [Fact]
    public void FlagRuleDto_RoundTrips_BothKinds()
    {
        var field = FlagRuleDto.From(FlagRule.ForFieldValue("Level", "FATAL")).ToRule();
        var regex = FlagRuleDto.From(FlagRule.ForRegex("EXCEPTION")).ToRule();

        field.Kind.Should().Be(FlagRule.MatchKind.FieldValue);
        field.FieldName.Should().Be("Level");
        regex.Kind.Should().Be(FlagRule.MatchKind.Regex);
        regex.MatchValue.Should().Be("EXCEPTION");
    }

    [Fact]
    public void DefaultRuleSets_ProvideNonEmptyColorAndFlagRules()
    {
        DefaultRuleSets.ColorRules().Should().NotBeEmpty();
        DefaultRuleSets.FlagRules().Should().NotBeEmpty();
    }

    [Fact]
    public void NewSettings_SeedDefaultColorAndFlagRules()
    {
        var settings = new AppSettings();

        settings.ColorRules.Should().NotBeEmpty();
        settings.FlagRules.Should().NotBeEmpty();
    }
}
