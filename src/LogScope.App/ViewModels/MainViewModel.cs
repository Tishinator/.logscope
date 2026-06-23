using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using LogScope.App.Mvvm;
using LogScope.App.Views;
using LogScope.Core.Documents;
using LogScope.Core.Parsing;
using LogScope.Core.Persistence;
using LogScope.Core.Workspace;

namespace LogScope.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly WorkspaceScanner _scanner = new();
    private readonly SettingsStore _settingsStore;
    private readonly ProfileRepository _profileRepo;
    private readonly ProfileResolver _resolver = new();

    public AppSettings Settings { get; }

    public ObservableCollection<WorkspaceNodeViewModel> WorkspaceRoots { get; } = [];
    public ObservableCollection<LogTabViewModel> OpenTabs { get; } = [];
    public ObservableCollection<LogProfile> SavedProfiles { get; } = [];

    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand RevealInExplorerCommand { get; }
    public RelayCommand OpenInEditorCommand { get; }
    public RelayCommand EditExtensionsCommand { get; }
    public RelayCommand NewProfileCommand { get; }
    public RelayCommand ImportProfileCommand { get; }
    public RelayCommand EditParserCommand { get; }
    public RelayCommand ResetSettingsCommand { get; }
    public RelayCommand AssignProfileToFileCommand { get; }
    public RelayCommand AssignProfileToFolderCommand { get; }
    public RelayCommand OpenInNewTabCommand { get; }

    public MainViewModel()
    {
        AppPaths.EnsureCreated();
        _settingsStore = new SettingsStore(AppPaths.SettingsFile);
        _profileRepo = new ProfileRepository(AppPaths.ProfilesDirectory);
        Settings = _settingsStore.Load();

        foreach (var p in _profileRepo.LoadAll())
            SavedProfiles.Add(p);

        OpenFileCommand = new RelayCommand(OpenFile);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        RevealInExplorerCommand = new RelayCommand(p => RevealInExplorer((p as WorkspaceNodeViewModel)?.FilePath ?? p as string));
        OpenInEditorCommand = new RelayCommand(p => OpenInEditor((p as WorkspaceNodeViewModel)?.FilePath ?? p as string));
        OpenInNewTabCommand = new RelayCommand(p => { if ((p as WorkspaceNodeViewModel)?.FilePath is { } f) OpenLog(f, forceNewTab: true); });
        EditExtensionsCommand = new RelayCommand(EditExtensions);
        NewProfileCommand = new RelayCommand(() => LaunchParserWizard(SelectedTab?.FilePath));
        ImportProfileCommand = new RelayCommand(ImportProfile);
        EditParserCommand = new RelayCommand(() => LaunchParserWizard(SelectedTab?.FilePath, SelectedTab));
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        AssignProfileToFileCommand = new RelayCommand(p => AssignProfile(p, toFolder: false));
        AssignProfileToFolderCommand = new RelayCommand(p => AssignProfile(p, toFolder: true));
    }

    private string? _workspacePath;
    public string? WorkspacePath { get => _workspacePath; private set { SetField(ref _workspacePath, value); OnPropertyChanged(nameof(HasWorkspace)); } }
    public bool HasWorkspace => WorkspaceRoots.Count > 0;

    private LogTabViewModel? _selectedTab;
    public LogTabViewModel? SelectedTab { get => _selectedTab; set => SetField(ref _selectedTab, value); }

    public string ExtensionsDisplay => string.Join(", ", Settings.IncludedExtensions);

    // ----- Opening -----

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
            var result = _scanner.Scan(folder, Settings.IncludedExtensions);
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

    public void OpenLog(string filePath, bool forceNewTab = false)
    {
        var existing = OpenTabs.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null && !forceNewTab)
        {
            // UR-03: offer to focus the open tab or open another copy.
            var choice = MessageBox.Show(
                $"{Path.GetFileName(filePath)} is already open.\n\nYes = focus the open tab, No = open in a new tab.",
                "Already open", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) return;
            if (choice == MessageBoxResult.Yes) { SelectedTab = existing; return; }
        }

        try
        {
            var profile = ResolveProfile(filePath);
            var doc = LogDocument.Load(filePath, profile, encoding: null, LogDocument.DefaultMaxLines);
            var tab = new LogTabViewModel(doc);
            OpenTabs.Add(tab);
            SelectedTab = tab;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open log:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Per-file override > directory assignment > auto-detection (UR-05/UR-06).</summary>
    private LogProfile ResolveProfile(string filePath)
    {
        var name = _resolver.Resolve(filePath, Settings.DirectoryProfileAssignments, Settings.FileProfileOverrides);
        if (name != null)
        {
            var saved = SavedProfiles.FirstOrDefault(p => p.Name == name);
            if (saved != null) return saved;
        }
        return DetectProfile(filePath);
    }

    private static LogProfile DetectProfile(string filePath)
    {
        var sample = File.ReadLines(filePath).Take(50).ToList();
        var suggestion = new FormatDetector().Detect(sample);
        if (suggestion.Kind == DetectedFormatKind.Delimited && suggestion.Delimiter != null)
        {
            var p = LogProfile.Delimited(suggestion.Delimiter, suggestion.SuggestedFieldNames.ToList());
            p.Name = "Auto-detected";
            return p;
        }
        var raw = LogProfile.Raw();
        raw.Name = "Raw";
        return raw;
    }

    public void CloseTab(LogTabViewModel tab)
    {
        tab.Dispose();
        OpenTabs.Remove(tab);
    }

    // ----- Profiles -----

    private void LaunchParserWizard(string? filePath, LogTabViewModel? applyTo = null)
    {
        if (filePath == null || !File.Exists(filePath))
        {
            MessageBox.Show("Open a log file first, then configure a parser for it.", "No file",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sample = SafeSample(filePath);
        var wizard = new ParserWizardWindow(sample) { Owner = Application.Current?.MainWindow };
        if (wizard.ShowDialog() != true || wizard.ResultProfile == null)
            return;

        var profile = wizard.ResultProfile;

        if (wizard.SaveToLibrary)
        {
            _profileRepo.Save(profile);
            RefreshSavedProfiles();
        }

        // Apply to the current tab.
        var tab = applyTo ?? OpenTabs.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (tab != null)
        {
            try
            {
                tab.ApplyDocument(LogDocument.Load(filePath, profile, encoding: null, LogDocument.DefaultMaxLines));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not apply profile:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ImportProfile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import parser profile",
            Filter = "Profile files (*.json;*.logscopeprofile)|*.json;*.logscopeprofile|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var profile = _profileRepo.Import(dlg.FileName);
            _profileRepo.Save(profile);
            RefreshSavedProfiles();
            MessageBox.Show($"Imported profile '{profile.Name}'.", "Imported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not import profile:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void ExportProfile(LogProfile profile)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export parser profile",
            FileName = profile.Name + ".logscopeprofile",
            Filter = "Profile files (*.logscopeprofile)|*.logscopeprofile|JSON (*.json)|*.json",
        };
        if (dlg.ShowDialog() == true)
            _profileRepo.Export(profile, dlg.FileName);
    }

    private void AssignProfile(object? param, bool toFolder)
    {
        if (SelectedTab == null) return;
        if (param is not LogProfile profile) return;

        if (toFolder)
        {
            var dir = Path.GetDirectoryName(SelectedTab.FilePath);
            if (dir != null) Settings.DirectoryProfileAssignments[dir] = profile.Name;
        }
        else
        {
            Settings.FileProfileOverrides[SelectedTab.FilePath] = profile.Name;
        }
        SaveSettings();

        try { SelectedTab.ApplyDocument(LogDocument.Load(SelectedTab.FilePath, profile, null, LogDocument.DefaultMaxLines)); }
        catch { /* ignore */ }
    }

    private void RefreshSavedProfiles()
    {
        SavedProfiles.Clear();
        foreach (var p in _profileRepo.LoadAll())
            SavedProfiles.Add(p);
    }

    // ----- Extensions & settings -----

    private void EditExtensions()
    {
        var current = string.Join(", ", Settings.IncludedExtensions);
        var input = new InputDialog("Included file extensions",
            "Comma-separated list (e.g. .log, .txt, .out):", current) { Owner = Application.Current?.MainWindow };
        if (input.ShowDialog() != true) return;

        var exts = input.ResponseText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (exts.Count == 0) exts.Add(".log");

        Settings.IncludedExtensions = exts;
        SaveSettings();
        OnPropertyChanged(nameof(ExtensionsDisplay));

        if (WorkspacePath != null && Directory.Exists(WorkspacePath))
            LoadWorkspace(WorkspacePath);
    }

    private void ResetSettings()
    {
        var confirm = MessageBox.Show(
            "Reset all settings (extensions, profile assignments, presets, window layout)?\nLog files and workspaces are not affected.",
            "Reset settings", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        _settingsStore.Reset();
        var fresh = _settingsStore.Load();
        Settings.IncludedExtensions = fresh.IncludedExtensions;
        Settings.DirectoryProfileAssignments.Clear();
        Settings.FileProfileOverrides.Clear();
        Settings.FilterPresets.Clear();
        OnPropertyChanged(nameof(ExtensionsDisplay));
    }

    public void SaveSettings() => _settingsStore.Save(Settings);

    private static IReadOnlyList<string> SafeSample(string filePath)
    {
        try { return File.ReadLines(filePath).Take(200).ToList(); }
        catch { return []; }
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
