using FluentAssertions;
using LogScope.Core.Documents;

namespace LogScope.Core.Tests;

public class FieldSemanticsTests
{
    [Fact]
    public void NewProfile_HasGenericFieldTypes_ByDefault()
    {
        var profile = LogProfile.Delimited("|", ["Timestamp", "Level", "Message"]);

        profile.TypeOf("Level").Should().Be(FieldSemanticType.Generic);
    }

    [Fact]
    public void SetFieldType_AssignsSemanticType()
    {
        var profile = LogProfile.Delimited("|", ["Ts", "Lvl", "Msg"]);
        profile.SetFieldType("Ts", FieldSemanticType.Timestamp);
        profile.SetFieldType("Lvl", FieldSemanticType.Level);
        profile.SetFieldType("Msg", FieldSemanticType.Message);

        profile.TypeOf("Ts").Should().Be(FieldSemanticType.Timestamp);
        profile.TimestampField.Should().Be("Ts");
        profile.LevelField.Should().Be("Lvl");
        profile.MessageField.Should().Be("Msg");
    }

    [Fact]
    public void LevelOrder_HasSensibleStandardDefault()
    {
        var profile = LogProfile.Raw();
        profile.LevelOrder.Should().ContainInOrder("DEBUG", "INFO", "WARN", "ERROR");
    }

    [Fact]
    public void LevelRank_OrdersBySeverity_NotAlphabetically()
    {
        var profile = LogProfile.Raw();
        // ERROR is more severe than INFO even though "E" < "I" alphabetically
        profile.LevelRank("ERROR").Should().BeGreaterThan(profile.LevelRank("INFO"));
        profile.LevelRank("FATAL").Should().BeGreaterThan(profile.LevelRank("ERROR"));
    }

    [Fact]
    public void LevelRank_IsCaseInsensitive()
    {
        var profile = LogProfile.Raw();
        profile.LevelRank("error").Should().Be(profile.LevelRank("ERROR"));
    }

    [Fact]
    public void LevelRank_CustomOrder_IsRespected()
    {
        var profile = LogProfile.Raw();
        profile.LevelOrder = ["LOW", "MEDIUM", "HIGH", "CRITICAL"];

        profile.LevelRank("CRITICAL").Should().BeGreaterThan(profile.LevelRank("LOW"));
        profile.LevelRank("MEDIUM").Should().BeGreaterThan(profile.LevelRank("LOW"));
    }

    [Fact]
    public void LevelRank_UnknownLevel_SortsAfterKnownLevels()
    {
        var profile = LogProfile.Raw();
        profile.LevelRank("BANANAS").Should().BeGreaterThanOrEqualTo(profile.LevelOrder.Count);
    }

    [Fact]
    public void SemanticFields_DefaultToNull_WhenUnassigned()
    {
        var profile = LogProfile.Delimited("|", ["A", "B"]);
        profile.TimestampField.Should().BeNull();
        profile.LevelField.Should().BeNull();
    }
}
