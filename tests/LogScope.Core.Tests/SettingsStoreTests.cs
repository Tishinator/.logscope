using FluentAssertions;
using LogScope.Core.Persistence;

namespace LogScope.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(), "logscope_settings_" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var store = new SettingsStore(_file);

        var settings = store.Load();

        settings.Should().NotBeNull();
        settings.IncludedExtensions.Should().Contain(".log");
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var store = new SettingsStore(_file);
        var settings = store.Load();
        settings.WindowWidth = 1234;
        settings.WindowHeight = 777;
        settings.WindowMaximized = true;
        settings.IncludedExtensions = [".log", ".txt", ".out"];
        settings.StreamFollowByDefault = true;

        store.Save(settings);
        var reloaded = new SettingsStore(_file).Load();

        reloaded.WindowWidth.Should().Be(1234);
        reloaded.WindowHeight.Should().Be(777);
        reloaded.WindowMaximized.Should().BeTrue();
        reloaded.IncludedExtensions.Should().BeEquivalentTo([".log", ".txt", ".out"]);
        reloaded.StreamFollowByDefault.Should().BeTrue();
    }

    [Fact]
    public void SaveThenLoad_RoundTripsFilterPresets()
    {
        var store = new SettingsStore(_file);
        var settings = store.Load();
        settings.FilterPresets.Add(new FilterPreset("Errors only", "ERROR", IsRegex: false, OnlyFlagged: true));
        settings.FilterPresets.Add(new FilterPreset("Timeouts", @"time\w*out", IsRegex: true, OnlyFlagged: false));

        store.Save(settings);
        var reloaded = new SettingsStore(_file).Load();

        reloaded.FilterPresets.Should().HaveCount(2);
        reloaded.FilterPresets[0].Name.Should().Be("Errors only");
        reloaded.FilterPresets[1].IsRegex.Should().BeTrue();
    }

    [Fact]
    public void SaveThenLoad_RoundTripsProfileAssignments()
    {
        var store = new SettingsStore(_file);
        var settings = store.Load();
        settings.DirectoryProfileAssignments[@"C:\logs"] = "Pipe profile";
        settings.FileProfileOverrides[@"C:\logs\weird.log"] = "Regex profile";

        store.Save(settings);
        var reloaded = new SettingsStore(_file).Load();

        reloaded.DirectoryProfileAssignments[@"C:\logs"].Should().Be("Pipe profile");
        reloaded.FileProfileOverrides[@"C:\logs\weird.log"].Should().Be("Regex profile");
    }

    [Fact]
    public void SaveThenLoad_PreservesFlagRuleKind_AndField()
    {
        var store = new SettingsStore(_file);
        var settings = store.Load();
        settings.FlagRules =
        [
            new() { Kind = Core.Visualization.FlagRule.MatchKind.Regex, FieldName = "Level", MatchValue = "ERROR" },
            new() { Kind = Core.Visualization.FlagRule.MatchKind.Contains, FieldName = null, MatchValue = "boom" },
        ];

        store.Save(settings);
        var reloaded = new SettingsStore(_file).Load();

        // Regex must survive as Regex (guards against enum-ordinal serialization breakage).
        reloaded.FlagRules[0].Kind.Should().Be(Core.Visualization.FlagRule.MatchKind.Regex);
        reloaded.FlagRules[0].FieldName.Should().Be("Level");
        reloaded.FlagRules[1].Kind.Should().Be(Core.Visualization.FlagRule.MatchKind.Contains);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupt()
    {
        File.WriteAllText(_file, "{ this is not valid json ]");
        var store = new SettingsStore(_file);

        var settings = store.Load();

        settings.Should().NotBeNull();
        settings.IncludedExtensions.Should().Contain(".log");
    }

    [Fact]
    public void Reset_RemovesPersistedFile_AndNextLoadIsDefault()
    {
        var store = new SettingsStore(_file);
        var settings = store.Load();
        settings.WindowWidth = 999;
        store.Save(settings);
        File.Exists(_file).Should().BeTrue();

        store.Reset();

        File.Exists(_file).Should().BeFalse();
        store.Load().WindowWidth.Should().NotBe(999);
    }
}
