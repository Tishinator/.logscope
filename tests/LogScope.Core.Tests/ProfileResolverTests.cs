using FluentAssertions;
using LogScope.Core.Persistence;

namespace LogScope.Core.Tests;

public class ProfileResolverTests
{
    private static readonly Dictionary<string, string> Empty = new();

    [Fact]
    public void Resolve_ReturnsNull_WhenNoAssignments()
    {
        var resolver = new ProfileResolver();
        var name = resolver.Resolve(@"C:\logs\app.log", Empty, Empty);
        name.Should().BeNull();
    }

    [Fact]
    public void Resolve_UsesDirectoryAssignment_ForFileInThatDirectory()
    {
        var dirs = new Dictionary<string, string> { [@"C:\logs"] = "DirProfile" };
        var resolver = new ProfileResolver();

        resolver.Resolve(@"C:\logs\app.log", dirs, Empty).Should().Be("DirProfile");
    }

    [Fact]
    public void Resolve_DirectoryAssignmentApplies_ToSubfolders()
    {
        var dirs = new Dictionary<string, string> { [@"C:\logs"] = "DirProfile" };
        var resolver = new ProfileResolver();

        resolver.Resolve(@"C:\logs\sub\deeper\app.log", dirs, Empty).Should().Be("DirProfile");
    }

    [Fact]
    public void Resolve_PrefersNearestAncestorDirectory()
    {
        var dirs = new Dictionary<string, string>
        {
            [@"C:\logs"] = "Outer",
            [@"C:\logs\sub"] = "Inner",
        };
        var resolver = new ProfileResolver();

        resolver.Resolve(@"C:\logs\sub\app.log", dirs, Empty).Should().Be("Inner");
    }

    [Fact]
    public void Resolve_FileOverrideWins_OverDirectoryAssignment()
    {
        var dirs = new Dictionary<string, string> { [@"C:\logs"] = "DirProfile" };
        var files = new Dictionary<string, string> { [@"C:\logs\app.log"] = "FileProfile" };
        var resolver = new ProfileResolver();

        resolver.Resolve(@"C:\logs\app.log", dirs, files).Should().Be("FileProfile");
    }

    [Fact]
    public void Resolve_IsCaseInsensitive_OnWindowsPaths()
    {
        var dirs = new Dictionary<string, string> { [@"C:\Logs"] = "DirProfile" };
        var resolver = new ProfileResolver();

        resolver.Resolve(@"c:\logs\APP.LOG", dirs, Empty).Should().Be("DirProfile");
    }
}
