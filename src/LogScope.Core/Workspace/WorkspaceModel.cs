namespace LogScope.Core.Workspace;

public sealed class LogFileEntry
{
    public string FullPath { get; }
    public LogFileEntry(string fullPath) => FullPath = fullPath;
}

public sealed class FolderNode
{
    public string Name { get; }
    public string FullPath { get; }
    public List<FolderNode> SubFolders { get; } = [];
    public List<LogFileEntry> Files { get; } = [];

    public FolderNode(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
    }
}

public sealed class WorkspaceScanResult
{
    public FolderNode RootNode { get; }
    public IReadOnlyList<LogFileEntry> Files { get; }

    public WorkspaceScanResult(FolderNode rootNode, IReadOnlyList<LogFileEntry> files)
    {
        RootNode = rootNode;
        Files = files;
    }
}
