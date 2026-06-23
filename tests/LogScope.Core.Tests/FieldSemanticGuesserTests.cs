using FluentAssertions;
using LogScope.Core.Documents;

namespace LogScope.Core.Tests;

public class FieldSemanticGuesserTests
{
    [Theory]
    [InlineData("Timestamp", FieldSemanticType.Timestamp)]
    [InlineData("ts", FieldSemanticType.Timestamp)]
    [InlineData("Level", FieldSemanticType.Level)]
    [InlineData("severity", FieldSemanticType.Level)]
    [InlineData("Message", FieldSemanticType.Message)]
    [InlineData("Module", FieldSemanticType.Module)]
    [InlineData("ThreadId", FieldSemanticType.Thread)]
    [InlineData("DeviceId", FieldSemanticType.DeviceId)]
    [InlineData("Something", FieldSemanticType.Generic)]
    public void Guess_MapsNamesToTypes(string name, FieldSemanticType expected)
    {
        FieldSemanticGuesser.Guess(name).Should().Be(expected);
    }

    [Fact]
    public void ApplyGuessedTypes_TypesNamedFields_ButKeepsExplicitOnes()
    {
        var profile = LogProfile.Delimited("|", ["Timestamp", "Level", "Note"]);
        profile.SetFieldType("Note", FieldSemanticType.Message); // explicit, should be kept

        FieldSemanticGuesser.ApplyGuessedTypes(profile);

        profile.TypeOf("Timestamp").Should().Be(FieldSemanticType.Timestamp);
        profile.TypeOf("Level").Should().Be(FieldSemanticType.Level);
        profile.TypeOf("Note").Should().Be(FieldSemanticType.Message);
    }
}
