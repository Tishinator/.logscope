using FluentAssertions;
using LogScope.Core.Documents;
using LogScope.Core.Persistence;

namespace LogScope.Core.Tests;

public class ProfileRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "logscope_profiles_" + Guid.NewGuid().ToString("N"));

    public ProfileRepositoryTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void SaveAndLoadAll_RoundTripsDelimiterProfile()
    {
        var repo = new ProfileRepository(_dir);
        var profile = LogProfile.Delimited("||", ["Timestamp", "Level", "Message"]);
        profile.Name = "Pipe pipes";

        repo.Save(profile);
        var loaded = repo.LoadAll();

        loaded.Should().ContainSingle();
        loaded[0].Name.Should().Be("Pipe pipes");
        loaded[0].Kind.Should().Be(LogProfileKind.Delimited);
        loaded[0].Delimiter.Should().Be("||");
        loaded[0].FieldNames.Should().ContainInOrder("Timestamp", "Level", "Message");
    }

    [Fact]
    public void SaveAndLoadAll_RoundTripsRegexProfileWithMultiline()
    {
        var repo = new ProfileRepository(_dir);
        var profile = LogProfile.Regex(@"(?<Level>\w+):(?<Message>.+)").WithMultiline(@"^\d{4}-");
        profile.Name = "Regex one";

        repo.Save(profile);
        var loaded = repo.LoadAll().Single();

        loaded.Kind.Should().Be(LogProfileKind.Regex);
        loaded.Pattern.Should().Be(@"(?<Level>\w+):(?<Message>.+)");
        loaded.MultilineNewEventPattern.Should().Be(@"^\d{4}-");
    }

    [Fact]
    public void Save_OverwritesProfileWithSameName()
    {
        var repo = new ProfileRepository(_dir);
        repo.Save(Named(LogProfile.Delimited(",", ["A"]), "Dup"));
        repo.Save(Named(LogProfile.Delimited("|", ["B"]), "Dup"));

        var loaded = repo.LoadAll();
        loaded.Should().ContainSingle();
        loaded[0].Delimiter.Should().Be("|");
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        var repo = new ProfileRepository(_dir);
        repo.Save(Named(LogProfile.Raw(), "Throwaway"));
        repo.LoadAll().Should().ContainSingle();

        repo.Delete("Throwaway");

        repo.LoadAll().Should().BeEmpty();
    }

    [Fact]
    public void ExportThenImport_RoundTripsThroughArbitraryFile()
    {
        var repo = new ProfileRepository(_dir);
        var profile = Named(LogProfile.Delimited("\t", ["X", "Y"]), "Tabby");
        var exportPath = Path.Combine(_dir, "exported.logscopeprofile");

        repo.Export(profile, exportPath);
        var imported = repo.Import(exportPath);

        imported.Name.Should().Be("Tabby");
        imported.Delimiter.Should().Be("\t");
        imported.FieldNames.Should().ContainInOrder("X", "Y");
    }

    [Fact]
    public void SaveHandlesNamesWithInvalidFileChars()
    {
        var repo = new ProfileRepository(_dir);
        var profile = Named(LogProfile.Raw(), "weird/name:with*chars?");

        var act = () => repo.Save(profile);
        act.Should().NotThrow();
        repo.LoadAll().Should().ContainSingle().Which.Name.Should().Be("weird/name:with*chars?");
    }

    private static LogProfile Named(LogProfile p, string name) { p.Name = name; return p; }
}
