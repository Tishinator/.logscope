using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using LogScope.App.Mvvm;
using LogScope.App.Views;
using LogScope.Core.Documents;
using LogScope.Core.Parsing;
using LogScope.Core.Persistence;
using LogScope.Core.Sync;
using LogScope.Core.Visualization;
using LogScope.Core.Workspace;

namespace LogScope.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly WorkspaceScanner _scanner = new();
    private readonly SettingsStore _settingsStore;
    private readonly ProfileRepository _profileRepo;
    private readonly ProfileResolver _resolver = new();

    private FileSystemWatcher? _workspaceWatcher;
    private System.Threading.Timer? _refreshDebounce;

    public AppSettings Settings { get; }

    public ObservableCollection<WorkspaceNodeViewModel> WorkspaceRoots { get; } = [];
    public ObservableCollection<LogTabViewModel> OpenTabs { get; } = [];
    public ObservableCollection<LogTabViewModel> SplitGroup { get; } = [];
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
    public RelayCommand SaveFilterPresetCommand { get; }
    public RelayCommand ApplyFilterPresetCommand { get; }
    public RelayCommand ReloadEncodingCommand { get; }
    public RelayCommand EditColorRulesCommand { get; }
    public RelayCommand EditFlagRulesCommand { get; }
    public RelayCommand ToggleCompareCommand { get; }
    public RelayCommand ExitCompareCommand { get; }
    public RelayCommand SplitWithActiveCommand { get; }
    public RelayCommand RemoveFromSplitCommand { get; }
    public RelayCommand ExportProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand RenameProfileCommand { get; }

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
        SaveFilterPresetCommand = new RelayCommand(SaveFilterPreset);
        ApplyFilterPresetCommand = new RelayCommand(p => { if (p is FilterPreset fp) ApplyFilterPreset(fp); });
        ReloadEncodingCommand = new RelayCommand(p => ReloadWithEncoding(p as string));
        EditColorRulesCommand = new RelayCommand(EditColorRules);
        EditFlagRulesCommand = new RelayCommand(EditFlagRules);
        ToggleCompareCommand = new RelayCommand(ToggleCompareAll);
        ExitCompareCommand = new RelayCommand(ExitSplit);
        SplitWithActiveCommand = new RelayCommand(p => SplitWithActive(p as LogTabViewModel));
        RemoveFromSplitCommand = new RelayCommand(p => RemoveFromSplit(p as LogTabViewModel));
        ExportProfileCommand = new RelayCommand(p => { if (p is LogProfile pr) ExportProfile(pr); });
        DeleteProfileCommand = new RelayCommand(p => { if (p is LogProfile pr) DeleteProfile(pr); });
        RenameProfileCommand = new RelayCommand(p => { if (p is LogProfile pr) RenameProfile(pr); });

        OpenTabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanCompare));
    }

    /// <summary>Right-click a tab → split it with the active tab; repeating adds to the existing group.</summary>
    private void SplitWithActive(LogTabViewModel? tab)
    {
        if (tab == null) return;

        if (!CompareMode)
            SplitGroup.Clear();

        void AddUnique(LogTabViewModel? t)
        {
            if (t != null && !SplitGroup.Contains(t)) SplitGroup.Add(t);
        }

        // Right-clicking a tab selects it, so the "active" tab to split with is the previously
        // selected one when the clicked tab is now the active selection.
        var active = (tab == SelectedTab) ? _previousSelectedTab : SelectedTab;
        AddUnique(active);
        AddUnique(tab);

        if (SplitGroup.Count >= 2)
            CompareMode = true;
    }

    private void RemoveFromSplit(LogTabViewModel? tab)
    {
        if (tab == null) return;
        SplitGroup.Remove(tab);
        if (SplitGroup.Count < 2) ExitSplit();
    }

    /// <summary>View ▸ Split view: split all open logs at once (convenience).</summary>
    private void ToggleCompareAll()
    {
        if (CompareMode) { ExitSplit(); return; }
        if (!CanCompare) return;
        SplitGroup.Clear();
        foreach (var t in OpenTabs) SplitGroup.Add(t);
        CompareMode = true;
    }

    private void ExitSplit()
    {
        CompareMode = false;
        SplitGroup.Clear();
    }

    // ----- Synchronized side-by-side comparison (UR-13 / SR-09) -----

    private bool _compareMode;
    public bool CompareMode
    {
        get => _compareMode;
        set
        {
            if (value && !CanCompare) return; // split view requires 2+ logs
            if (SetField(ref _compareMode, value))
            {
                OnPropertyChanged(nameof(SingleMode));
                OnPropertyChanged(nameof(ShowSingleView));
                OnPropertyChanged(nameof(ShowNoTabHint));
            }
        }
    }
    public bool SingleMode => !_compareMode;

    /// <summary>Split view / sync only make sense with at least two logs open.</summary>
    public bool CanCompare => OpenTabs.Count >= 2;

    private SyncMode _syncMode = SyncMode.Off;
    public SyncMode SyncMode { get => _syncMode; set => SetField(ref _syncMode, value); }
    public Array SyncModes => Enum.GetValues(typeof(SyncMode));

    private bool _syncing;
    private bool _timestampWarningShown;

    private void OnTabSelectionChanged(LogTabViewModel source)
    {
        if (_syncing || !CompareMode || SyncMode == SyncMode.Off) return;
        if (!source.IsSyncEnabled || source.SelectedRow == null) return;

        _syncing = true;
        try
        {
            if (SyncMode == SyncMode.Timestamp)
                SyncByTimestamp(source);
            else
                SyncByLine(source);
        }
        finally { _syncing = false; }
    }

    private void SyncByLine(LogTabViewModel source)
    {
        int refLine = source.SelectedRow!.LineNumber;
        foreach (var tab in SplitGroup)
        {
            if (tab == source || !tab.IsSyncEnabled) continue;
            tab.SelectLine(SyncAligner.AlignByLine(refLine, tab.LastLineNumber));
        }
    }

    private void SyncByTimestamp(LogTabViewModel source)
    {
        var field = source.CurrentProfile.TimestampField;
        var refValue = field != null ? source.SelectedRow!.GetField(field) : null;
        var refTs = TimestampParser.TryParse(refValue);

        if (refTs == null)
        {
            WarnTimestampFallback();
            SyncByLine(source);
            return;
        }

        foreach (var tab in SplitGroup)
        {
            if (tab == source || !tab.IsSyncEnabled) continue;

            var rows = tab.TimestampedRows();
            if (rows.Count == 0)
            {
                WarnTimestampFallback();
                tab.SelectLine(SyncAligner.AlignByLine(source.SelectedRow!.LineNumber, tab.LastLineNumber));
                continue;
            }

            var line = SyncAligner.NearestByTimestamp(rows, refTs.Value);
            if (line.HasValue) tab.SelectLine(line.Value);
        }
    }

    private void WarnTimestampFallback()
    {
        if (_timestampWarningShown) return;
        _timestampWarningShown = true;
        MessageBox.Show(
            "One or more logs has no usable Timestamp field, so line-number synchronization is used as a fallback.\n\n" +
            "Tip: assign a field the 'Timestamp' type in the parser setup to enable timestamp sync.",
            "Timestamp sync", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ----- Color / flag rules (UR-10) -----

    public IReadOnlyList<ColorRule> ColorRules() => Settings.ColorRules.Select(d => d.ToRule()).ToList();
    public IReadOnlyList<FlagRule> FlagRules() => Settings.FlagRules.Select(d => d.ToRule()).ToList();

    private void EditColorRules()
    {
        var editor = new ColorRulesWindow(Settings.ColorRules) { Owner = Application.Current?.MainWindow };
        if (editor.ShowDialog() != true) return;

        Settings.ColorRules = editor.Rules.ToList();
        SaveSettings();

        ReapplyRulesToOpenTabs();
    }

    private void EditFlagRules()
    {
        var editor = new FlagRulesWindow(Settings.FlagRules) { Owner = Application.Current?.MainWindow };
        if (editor.ShowDialog() != true) return;

        Settings.FlagRules = editor.Rules.ToList();
        SaveSettings();
        ReapplyRulesToOpenTabs();
    }

    private void ReapplyRulesToOpenTabs()
    {
        var colors = ColorRules();
        var flags = FlagRules();
        foreach (var tab in OpenTabs)
            tab.UpdateRules(colors, flags);
    }

    // ----- Filter presets (UR-08) -----

    private void SaveFilterPreset()
    {
        if (SelectedTab == null) return;
        var input = new InputDialog("Save filter preset", "Preset name:", "My filter")
        {
            Owner = Application.Current?.MainWindow
        };
        if (input.ShowDialog() != true || string.IsNullOrWhiteSpace(input.ResponseText)) return;

        var preset = new FilterPreset(
            input.ResponseText.Trim(),
            SelectedTab.FilterText,
            SelectedTab.FilterIsRegex,
            SelectedTab.OnlyFlagged,
            string.IsNullOrEmpty(SelectedTab.FilterTimeFrom) ? null : SelectedTab.FilterTimeFrom,
            string.IsNullOrEmpty(SelectedTab.FilterTimeTo) ? null : SelectedTab.FilterTimeTo);
        Settings.FilterPresets.RemoveAll(p => p.Name == preset.Name);
        Settings.FilterPresets.Add(preset);
        SaveSettings();
        OnPropertyChanged(nameof(FilterPresets));
    }

    private void ApplyFilterPreset(FilterPreset preset)
    {
        if (SelectedTab == null) return;
        SelectedTab.FilterIsRegex = preset.IsRegex;
        SelectedTab.OnlyFlagged = preset.OnlyFlagged;
        SelectedTab.FilterText = preset.FilterText;
        SelectedTab.FilterTimeFrom = preset.FilterTimeFrom ?? string.Empty;
        SelectedTab.FilterTimeTo = preset.FilterTimeTo ?? string.Empty;
    }

    public IEnumerable<FilterPreset> FilterPresets => Settings.FilterPresets;

    // ----- Manual encoding override (SR-04) -----

    private void ReloadWithEncoding(string? encodingName)
    {
        if (SelectedTab == null || encodingName == null) return;
        var encoding = encodingName switch
        {
            "UTF-8" => System.Text.Encoding.UTF8,
            "UTF-16 LE" => System.Text.Encoding.Unicode,
            "UTF-16 BE" => System.Text.Encoding.BigEndianUnicode,
            "Windows-1252 (ANSI)" => System.Text.Encoding.GetEncoding(1252),
            _ => null,
        };
        try
        {
            SelectedTab.ApplyDocument(LogDocument.Load(SelectedTab.FilePath, SelectedTab.CurrentProfile, encoding, LogDocument.DefaultMaxLines));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not reload with {encodingName}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private string? _workspacePath;
    public string? WorkspacePath { get => _workspacePath; private set { SetField(ref _workspacePath, value); OnPropertyChanged(nameof(HasWorkspace)); } }
    public bool HasWorkspace => WorkspaceRoots.Count > 0;

    private LogTabViewModel? _selectedTab;
    private LogTabViewModel? _previousSelectedTab;
    public LogTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab == value) return;
            _previousSelectedTab = _selectedTab;
            SetField(ref _selectedTab, value);
            OnPropertyChanged(nameof(ShowSingleView));
            OnPropertyChanged(nameof(ShowNoTabHint));
        }
    }

    /// <summary>Single-tab content is shown only when not split and a tab is actually open.</summary>
    public bool ShowSingleView => SingleMode && SelectedTab != null;

    /// <summary>Hint shown when a workspace is open but no log tab is yet.</summary>
    public bool ShowNoTabHint => SingleMode && SelectedTab == null;

    public string ExtensionsDisplay => string.Join(", ", Settings.IncludedExtensions);

    /// <summary>Wraps Settings.StreamFollowByDefault so toggling auto-saves (SR-10).</summary>
    public bool StreamFollowByDefault
    {
        get => Settings.StreamFollowByDefault;
        set { Settings.StreamFollowByDefault = value; OnPropertyChanged(); SaveSettings(); }
    }

    public bool ShowIndicatorsInTree
    {
        get => Settings.ShowIndicatorsInTree;
        set { Settings.ShowIndicatorsInTree = value; OnPropertyChanged(); SaveSettings(); }
    }

    public bool ShowIndicatorsInSummary
    {
        get => Settings.ShowIndicatorsInSummary;
        set { Settings.ShowIndicatorsInSummary = value; OnPropertyChanged(); SaveSettings(); }
    }

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
            AttachWorkspaceWatcher(folder);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open workspace:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AttachWorkspaceWatcher(string folder)
    {
        _workspaceWatcher?.Dispose();
        _refreshDebounce?.Dispose();
        _refreshDebounce = null;

        var watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };

        void Schedule(object? sender, FileSystemEventArgs e)
        {
            _refreshDebounce?.Dispose();
            _refreshDebounce = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => LoadWorkspace(folder));
            }, null, 600, System.Threading.Timeout.Infinite);
        }

        watcher.Created += Schedule;
        watcher.Deleted += Schedule;
        watcher.Renamed += (s, e) => Schedule(s, e);
        _workspaceWatcher = watcher;
    }

    public void Dispose()
    {
        _workspaceWatcher?.Dispose();
        _refreshDebounce?.Dispose();
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
            var tab = new LogTabViewModel(doc, ColorRules(), FlagRules());
            if (Settings.StreamFollowByDefault)
                tab.StreamingEnabled = true;
            ApplyColumnLayout(tab);
            tab.PropertyChanged += OnTabPropertyChanged;
            tab.ColumnVisibilityChanged += (name, visible) => PersistColumnVisibility(tab, name, visible);
            tab.ColumnStateChanged += (name, width, displayIndex) => PersistColumnWidthOrder(tab, name, width, displayIndex);
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
            FieldSemanticGuesser.ApplyGuessedTypes(p); // type Timestamp/Level/etc. so sync + severity sort work
            return p;
        }
        var raw = LogProfile.Raw();
        raw.Name = "Raw";
        return raw;
    }

    public void CloseTab(LogTabViewModel tab)
    {
        tab.PropertyChanged -= OnTabPropertyChanged;
        tab.Dispose();
        SplitGroup.Remove(tab);
        OpenTabs.Remove(tab);
        if (CompareMode && SplitGroup.Count < 2) ExitSplit();
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not LogTabViewModel tab) return;
        if (e.PropertyName == nameof(LogTabViewModel.SelectedRow))
            OnTabSelectionChanged(tab);
        if (e.PropertyName == nameof(LogTabViewModel.FlaggedCount) && Settings.ShowIndicatorsInTree)
            PushFlaggedCountToTree(tab.FilePath, tab.FlaggedCount);
    }

    private void PushFlaggedCountToTree(string filePath, int count)
    {
        static WorkspaceNodeViewModel? Find(IEnumerable<WorkspaceNodeViewModel> nodes, string path)
        {
            foreach (var n in nodes)
            {
                if (n.IsFile && string.Equals(n.FilePath, path, StringComparison.OrdinalIgnoreCase))
                    return n;
                var found = Find(n.Children, path);
                if (found != null) return found;
            }
            return null;
        }

        var node = Find(WorkspaceRoots, filePath);
        if (node != null) node.FlaggedCount = count;
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

    private void DeleteProfile(LogProfile profile)
    {
        var confirm = MessageBox.Show(
            $"Delete profile '{profile.Name}'?\nAny directory or file assignments using this profile will no longer resolve.",
            "Delete profile", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        _profileRepo.Delete(profile.Name);

        // Clear any assignments that referenced it
        foreach (var key in Settings.DirectoryProfileAssignments.Keys.Where(k => Settings.DirectoryProfileAssignments[k] == profile.Name).ToList())
            Settings.DirectoryProfileAssignments.Remove(key);
        foreach (var key in Settings.FileProfileOverrides.Keys.Where(k => Settings.FileProfileOverrides[k] == profile.Name).ToList())
            Settings.FileProfileOverrides.Remove(key);
        SaveSettings();
        RefreshSavedProfiles();
    }

    private void RenameProfile(LogProfile profile)
    {
        var input = new InputDialog("Rename profile", "New name:", profile.Name)
        {
            Owner = Application.Current?.MainWindow
        };
        if (input.ShowDialog() != true || string.IsNullOrWhiteSpace(input.ResponseText)) return;

        var newName = input.ResponseText.Trim();
        var oldName = profile.Name;
        if (newName == oldName) return;

        profile.Name = newName;
        _profileRepo.Save(profile);
        _profileRepo.Delete(oldName);

        // Update any assignments that referenced the old name
        foreach (var key in Settings.DirectoryProfileAssignments.Keys.Where(k => Settings.DirectoryProfileAssignments[k] == oldName).ToList())
            Settings.DirectoryProfileAssignments[key] = newName;
        foreach (var key in Settings.FileProfileOverrides.Keys.Where(k => Settings.FileProfileOverrides[k] == oldName).ToList())
            Settings.FileProfileOverrides[key] = newName;
        SaveSettings();
        RefreshSavedProfiles();
    }

    private void RefreshSavedProfiles()
    {
        SavedProfiles.Clear();
        foreach (var p in _profileRepo.LoadAll())
            SavedProfiles.Add(p);
    }

    // ----- Column show/hide persistence (SR-10) -----

    private static string ColumnLayoutKey(LogTabViewModel tab) =>
        string.IsNullOrWhiteSpace(tab.CurrentProfile.Name) ? "Auto" : tab.CurrentProfile.Name;

    private void ApplyColumnLayout(LogTabViewModel tab)
    {
        if (!Settings.ColumnLayouts.TryGetValue(ColumnLayoutKey(tab), out var layout)) return;
        foreach (var toggle in tab.ColumnToggles)
        {
            if (!layout.TryGetValue(toggle.Name, out var state)) continue;
            toggle.IsVisible = state.Visible;
            if (state.Width > 0 || state.DisplayIndex > 0)
                tab.SavedColumnGeometry[toggle.Name] = (state.Width, state.DisplayIndex);
        }
    }

    private void PersistColumnVisibility(LogTabViewModel tab, string column, bool visible)
    {
        var key = ColumnLayoutKey(tab);
        if (!Settings.ColumnLayouts.TryGetValue(key, out var layout))
            Settings.ColumnLayouts[key] = layout = new();
        if (!layout.TryGetValue(column, out var state))
            layout[column] = state = new ColumnState();
        state.Visible = visible;
        SaveSettings();
    }

    private void PersistColumnWidthOrder(LogTabViewModel tab, string column, double width, int displayIndex)
    {
        var key = ColumnLayoutKey(tab);
        if (!Settings.ColumnLayouts.TryGetValue(key, out var layout))
            Settings.ColumnLayouts[key] = layout = new();
        if (!layout.TryGetValue(column, out var state))
            layout[column] = state = new ColumnState();
        state.Width = width;
        state.DisplayIndex = displayIndex;
        SaveSettings();
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
            "Reset all settings (extensions, profile assignments, presets, color/flag rules, column layouts, window layout)?\nLog files and workspaces are not affected.",
            "Reset settings", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        _settingsStore.Reset();
        var fresh = _settingsStore.Load();

        // Copy ALL fields from the fresh defaults into the live Settings object.
        Settings.IncludedExtensions = fresh.IncludedExtensions;
        Settings.DirectoryProfileAssignments.Clear();
        Settings.FileProfileOverrides.Clear();
        Settings.FilterPresets.Clear();
        Settings.ColorRules = fresh.ColorRules;
        Settings.FlagRules = fresh.FlagRules;
        Settings.ColumnLayouts.Clear();
        Settings.StreamFollowByDefault = fresh.StreamFollowByDefault;
        Settings.ShowIndicatorsInTree = fresh.ShowIndicatorsInTree;
        Settings.ShowIndicatorsInSummary = fresh.ShowIndicatorsInSummary;

        OnPropertyChanged(nameof(ExtensionsDisplay));
        OnPropertyChanged(nameof(FilterPresets));

        // Re-apply default rules to all open tabs immediately.
        ReapplyRulesToOpenTabs();
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
