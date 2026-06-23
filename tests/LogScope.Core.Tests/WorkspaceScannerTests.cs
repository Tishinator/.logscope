using FluentAssertions;
using LogScope.Core.Workspace;

namespace LogScope.Core.Tests;

public class WorkspaceScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "logscope_ws_" + Guid.NewGuid().ToString("N"));

    public WorkspaceScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Touch(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "");
    }

    [Fact]
    public void Scan_FindsLogFiles_AtRootLevel()
    {
        Touch("app.log");
        Touch("system.log");
        Touch("notes.txt");

        var scanner = new WorkspaceScanner();
        var result = scanner.Scan(_root);

        result.Files.Select(f => Path.GetFileName(f.FullPath))
            .Should().BeEquivalentTo("app.log", "system.log");
    }

    [Fact]
    public void Scan_RecursesIntoSubdirectories()
    {
        Touch("app.log");
        Touch(Path.Combine("sub", "nested.log"));
        Touch(Path.Combine("sub", "deeper", "deep.log"));

        var scanner = new WorkspaceScanner();
        var result = scanner.Scan(_root);

        result.Files.Should().HaveCount(3);
    }

    [Fact]
    public void Scan_ExcludesNonLogExtensions_ByDefault()
    {
        Touch("app.log");
        Touch("data.csv");
        Touch("readme.txt");

        var scanner = new WorkspaceScanner();
        var result = scanner.Scan(_root);

        result.Files.Should().ContainSingle()
            .Which.FullPath.Should().EndWith("app.log");
    }

    [Fact]
    public void Scan_IncludesAdditionalExtensions_WhenConfigured()
    {
        Touch("app.log");
        Touch("data.csv");
        Touch("readme.txt");

        var scanner = new WorkspaceScanner();
        var result = scanner.Scan(_root, includedExtensions: [".log", ".txt"]);

        result.Files.Select(f => Path.GetFileName(f.FullPath))
            .Should().BeEquivalentTo("app.log", "readme.txt");
    }

    [Fact]
    public void Scan_BuildsTreeStructure_ReflectingFolders()
    {
        Touch("root.log");
        Touch(Path.Combine("sub", "child.log"));

        var scanner = new WorkspaceScanner();
        var result = scanner.Scan(_root);

        result.RootNode.Name.Should().Be(Path.GetFileName(_root));
        result.RootNode.Files.Should().ContainSingle(f => Path.GetFileName(f.FullPath) == "root.log");
        result.RootNode.SubFolders.Should().ContainSingle(d => d.Name == "sub");
        result.RootNode.SubFolders.Single().Files
            .Should().ContainSingle(f => Path.GetFileName(f.FullPath) == "child.log");
    }

    [Fact]
    public void Scan_ExtensionMatchIsCaseInsensitive()
    {
        Touch("UPPER.LOG");

        var scanner = new WorkspaceScanner();
        var result = scanner.Scan(_root);

        result.Files.Should().ContainSingle();
    }

    [Fact]
    public void Scan_ThrowsForMissingDirectory()
    {
        var scanner = new WorkspaceScanner();
        var act = () => scanner.Scan(Path.Combine(_root, "does-not-exist"));

        act.Should().Throw<DirectoryNotFoundException>();
    }
}
