using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using LogScope.Core.Workspace;

namespace LogScope.App.ViewModels;

/// <summary>
/// A node in the workspace tree — either a folder (with children) or a log file (a leaf).
/// </summary>
public sealed class WorkspaceNodeViewModel : INotifyPropertyChanged
{
    public string Name { get; }
    public string? FilePath { get; }
    public bool IsFile => FilePath != null;
    public bool IsExpanded { get; set; }
    public ObservableCollection<WorkspaceNodeViewModel> Children { get; } = [];

    private int _flaggedCount;
    public int FlaggedCount
    {
        get => _flaggedCount;
        set { if (_flaggedCount != value) { _flaggedCount = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Hover details (UR-02): path, size, modified date — enriched with profile/streaming when a tab is open.</summary>
    private string? _tooltip;
    public string? Tooltip
    {
        get => _tooltip;
        set { if (_tooltip != value) { _tooltip = value; OnPropertyChanged(); } }
    }

    private string? _baseTooltip;

    public void UpdateTabInfo(string? profileName, bool streaming)
    {
        if (_baseTooltip == null) return;
        var extra = new System.Text.StringBuilder(_baseTooltip);
        if (!string.IsNullOrEmpty(profileName))
            extra.Append($"\nProfile: {profileName}");
        if (streaming)
            extra.Append("\nStreaming: active");
        Tooltip = extra.ToString();
    }

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
                _baseTooltip = $"{filePath}\nSize: {FormatSize(info.Length)}\nModified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
            }
            catch
            {
                _baseTooltip = filePath;
            }
            _tooltip = _baseTooltip;
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
