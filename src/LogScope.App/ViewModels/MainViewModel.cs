using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using LogScope.App.Mvvm;
using LogScope.Core.Documents;
using LogScope.Core.Parsing;
using LogScope.Core.Workspace;

namespace LogScope.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly WorkspaceScanner _scanner = new();

    public ObservableCollection<WorkspaceNodeViewModel> WorkspaceRoots { get; } = [];
    public ObservableCollection<LogTabViewModel> OpenTabs { get; } = [];

    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand RevealInExplorerCommand { get; }
    public RelayCommand OpenInEditorCommand { get; }

    public MainViewModel()
    {
        OpenFileCommand = new RelayCommand(OpenFile);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        RevealInExplorerCommand = new RelayCommand(p => RevealInExplorer(p as string));
        OpenInEditorCommand = new RelayCommand(p => OpenInEditor(p as string));
    }

    private string? _workspacePath;
    public string? WorkspacePath { get => _workspacePath; private set { SetField(ref _workspacePath, value); OnPropertyChanged(nameof(HasWorkspace)); } }
    public bool HasWorkspace => WorkspaceRoots.Count > 0;

    private LogTabViewModel? _selectedTab;
    public LogTabViewModel? SelectedTab { get => _selectedTab; set => SetField(ref _selectedTab, value); }

    private void OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open log file",
            Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
            OpenSingleFile(dlg.FileName);
    }

    public void OpenSingleFile(string filePath)
    {
        WorkspaceRoots.Clear();
        WorkspaceRoots.Add(WorkspaceNodeViewModel.ForSingleFile(filePath));
        WorkspacePath = Path.GetDirectoryName(filePath);
        OnPropertyChanged(nameof(HasWorkspace));
        OpenLog(filePath);
    }

    private void OpenFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Open workspace folder" };
        if (dlg.ShowDialog() == true)
            LoadWorkspace(dlg.FolderName);
    }

    public void LoadWorkspace(string folder)
    {
        try
        {
            var result = _scanner.Scan(folder);
            WorkspaceRoots.Clear();
            WorkspaceRoots.Add(WorkspaceNodeViewModel.FromFolder(result.RootNode, expandRoot: true));
            WorkspacePath = folder;
            OnPropertyChanged(nameof(HasWorkspace));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open workspace:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void OpenLog(string filePath)
    {
        // Focus an already-open tab if present.
        var existing = OpenTabs.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectedTab = existing;
            return;
        }

        try
        {
            var profile = DetectProfile(filePath);
            var doc = LogDocument.Load(filePath, profile);
            var tab = new LogTabViewModel(doc);
            OpenTabs.Add(tab);
            SelectedTab = tab;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open log:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static LogProfile DetectProfile(string filePath)
    {
        // Sample the first lines to suggest a format.
        var sample = File.ReadLines(filePath).Take(50).ToList();
        var suggestion = new FormatDetector().Detect(sample);

        if (suggestion.Kind == DetectedFormatKind.Delimited && suggestion.Delimiter != null)
        {
            return LogProfile.Delimited(suggestion.Delimiter, suggestion.SuggestedFieldNames.ToList());
        }

        return LogProfile.Raw();
    }

    public void CloseTab(LogTabViewModel tab)
    {
        tab.Dispose();
        OpenTabs.Remove(tab);
    }

    private static void RevealInExplorer(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private static void OpenInEditor(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }
}
