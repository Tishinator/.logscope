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

    private WorkspaceNodeViewModel(string name, string? filePath, bool expanded)
    {
        Name = name;
        FilePath = filePath;
        IsExpanded = expanded;
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
