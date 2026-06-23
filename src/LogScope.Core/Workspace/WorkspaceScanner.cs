namespace LogScope.Core.Workspace;

public sealed class WorkspaceScanner
{
    private static readonly string[] DefaultExtensions = [".log"];

    public WorkspaceScanResult Scan(string rootPath, IEnumerable<string>? includedExtensions = null)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Workspace directory not found: {rootPath}");

        var extensions = new HashSet<string>(
            includedExtensions ?? DefaultExtensions,
            StringComparer.OrdinalIgnoreCase);

        var allFiles = new List<LogFileEntry>();
        var rootNode = BuildNode(rootPath, extensions, allFiles);

        return new WorkspaceScanResult(rootNode, allFiles);
    }

    private static FolderNode BuildNode(string dirPath, HashSet<string> extensions, List<LogFileEntry> allFiles)
    {
        var node = new FolderNode(Path.GetFileName(dirPath.TrimEnd(Path.DirectorySeparatorChar)), dirPath);

        foreach (var file in Directory.EnumerateFiles(dirPath))
        {
            if (extensions.Contains(Path.GetExtension(file)))
            {
                var entry = new LogFileEntry(file);
                node.Files.Add(entry);
                allFiles.Add(entry);
            }
        }

        foreach (var subDir in Directory.EnumerateDirectories(dirPath))
        {
            var subNode = BuildNode(subDir, extensions, allFiles);
            node.SubFolders.Add(subNode);
        }

        return node;
    }
}
