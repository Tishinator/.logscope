using System.Collections.ObjectModel;
using System.IO;
using LogScope.Core.Workspace;

namespace LogScope.App.ViewModels;

/// <summary>
/// A node in the workspace tree — either a folder (with children) or a log file (a leaf).
/// </summary>
public sealed class WorkspaceNodeViewModel
{
    public string Name { get; }
    public string? FilePath { get; }
    public bool IsFile => FilePath != null;
    public bool IsExpanded { get; set; }
    public ObservableCollection<WorkspaceNodeViewModel> Children { get; } = [];

    /// <summary>Hover details (UR-02): size and modified date kept out of the always-on tree.</summary>
    public string? Tooltip { get; }

    private WorkspaceNodeViewModel(string name, string? filePath, bool expanded)
    {
        Name = name;
        FilePath = filePath;
        IsExpanded = expanded;

        if (filePath != null)
        {
            try
            {
                var info = new FileInfo(filePath);
                Tooltip = $"{filePath}\nSize: {FormatSize(info.Length)}\nModified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
            }
            catch
            {
                Tooltip = filePath;
            }
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.#} {units[unit]}";
    }

    public static WorkspaceNodeViewModel FromFolder(FolderNode node, bool expandRoot = false)
    {
        var vm = new WorkspaceNodeViewModel(node.Name, filePath: null, expanded: expandRoot);
        foreach (var sub in node.SubFolders.OrderBy(s => s.Name))
            vm.Children.Add(FromFolder(sub));
        foreach (var file in node.Files.OrderBy(f => Path.GetFileName(f.FullPath)))
            vm.Children.Add(new WorkspaceNodeViewModel(Path.GetFileName(file.FullPath), file.FullPath, expanded: false));
        return vm;
    }

    public static WorkspaceNodeViewModel ForSingleFile(string filePath) =>
        new(Path.GetFileName(filePath), filePath, expanded: false);
}
