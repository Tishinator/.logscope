using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using LogScope.App.Mvvm;
using LogScope.Core.Documents;
using LogScope.Core.Filtering;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;
using LogScope.Core.Search;
using LogScope.Core.Streaming;
using LogScope.Core.Visualization;

namespace LogScope.App.ViewModels;

public sealed class LogTabViewModel : ViewModelBase, IDisposable
{
    private ColorRuleEngine _colorEngine;
    private FlagRuleEngine _flagEngine;
    private readonly SearchEngine _searchEngine = new();

    private LogDocument _document;
    private LogStreamWatcher? _watcher;

    // Mutable mirrors of the document content so streaming can append without a full reload.
    private List<ParsedRow> _liveRows = [];
    private List<RawLogLine> _liveRaw = [];
    private int _parsedCount;
    private int _fallbackCount;

    public string Title { get; }
    public string FilePath { get; }
    public IReadOnlyList<string> Columns { get; private set; }
    public ObservableCollection<LogRowViewModel> Rows { get; } = [];

    /// <summary>Show/hide chooser entries for each column (SR-10). "Line" plus the data columns.</summary>
    public ObservableCollection<ColumnToggle> ColumnToggles { get; } = [];

    /// <summary>Raised when a column's visibility changes (name, isVisible) for the view + persistence.</summary>
    public event Action<string, bool>? ColumnVisibilityChanged;

    /// <summary>Raised by the view when a column is resized or reordered (name, width, displayIndex).</summary>
    public event Action<string, double, int>? ColumnStateChanged;

    /// <summary>Called from the view when a column's width or display-index changes.</summary>
    public void ReportColumnState(string name, double width, int displayIndex) =>
        ColumnStateChanged?.Invoke(name, width, displayIndex);

    /// <summary>Column names hidden when this tab was created (from persisted layout).</summary>
    public ISet<string> InitiallyHidden { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Persisted width + displayIndex for each column; populated by MainViewModel before the view renders.</summary>
    public Dictionary<string, (double Width, int DisplayIndex)> SavedColumnGeometry { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsColumnVisible(string name) =>
        ColumnToggles.FirstOrDefault(c => c.Name == name)?.IsVisible ?? true;

    private void BuildColumnToggles()
    {
        ColumnToggles.Clear();
        AddToggle("Line");
        foreach (var col in Columns)
            AddToggle(col);

        SearchFields.Clear();
        SearchFields.Add(AllFields);
        foreach (var col in Columns)
            SearchFields.Add(col);
        if (!SearchFields.Contains(SearchField))
            SearchField = AllFields;
    }

    private void AddToggle(string name)
    {
        var visible = !InitiallyHidden.Contains(name);
        ColumnToggles.Add(new ColumnToggle(name, visible,
            t => ColumnVisibilityChanged?.Invoke(t.Name, t.IsVisible)));
    }

    public string ProfileName => string.IsNullOrWhiteSpace(_document.Profile.Name) ? "Auto" : _document.Profile.Name;
    public LogProfile CurrentProfile => _document.Profile;
    public string EncodingName => _document.EncodingName;
    public string? EncodingWarning => _document.EncodingWarning;

    public RelayCommand CopyRowsCommand { get; }
    public RelayCommand CopyRawCommand { get; }
    public RelayCommand CopyLineRefsCommand { get; }

    /// <summary>Raised when the user asks to restore original file order; the view clears the grid sort.</summary>
    public event Action? RestoreOrderRequested;
    public RelayCommand RestoreOrderCommand { get; }

    /// <summary>The rows currently selected in the grid (set by the view for copy operations).</summary>
    public IReadOnlyList<LogRowViewModel> SelectedRows { get; set; } = [];

    public LogTabViewModel(LogDocument document, IEnumerable<ColorRule> colorRules, IEnumerable<FlagRule> flagRules)
    {
        _document = document;
        FilePath = document.FilePath;
        Title = Path.GetFileName(document.FilePath);
        Columns = document.Columns;
        _colorEngine = new ColorRuleEngine(colorRules);
        _flagEngine = new FlagRuleEngine(flagRules);
        InitLive();
        BuildColumnToggles();

        FindNextCommand = new RelayCommand(FindNext);
        FindPrevCommand = new RelayCommand(FindPrev);
        CopyRowsCommand = new RelayCommand(() => Copy(CopyMode.Tsv));
        CopyRawCommand = new RelayCommand(() => Copy(CopyMode.Raw));
        CopyLineRefsCommand = new RelayCommand(() => Copy(CopyMode.LineRefs));
        RestoreOrderCommand = new RelayCommand(() => RestoreOrderRequested?.Invoke());

        RefreshView();
    }

    /// <summary>Re-applies edited color/flag rules to the current view (UR-10).</summary>
    public void UpdateRules(IEnumerable<ColorRule> colorRules, IEnumerable<FlagRule> flagRules)
    {
        _colorEngine = new ColorRuleEngine(colorRules);
        _flagEngine = new FlagRuleEngine(flagRules);
        RefreshView();
    }

    private void InitLive()
    {
        _liveRows = _document.Rows.ToList();
        _liveRaw = _document.RawLines.ToList();
        _parsedCount = _document.ParsedCount;
        _fallbackCount = _document.FallbackCount;
    }

    /// <summary>Raised when the column set changes so the view can rebuild the grid columns.</summary>
    public event Action? ColumnsChanged;

    /// <summary>Replaces the underlying document (e.g. after applying a new parser profile).</summary>
    public void ApplyDocument(LogDocument document)
    {
        _document = document;
        InitLive();
        Columns = document.Columns;
        BuildColumnToggles();
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(RawText));
        OnPropertyChanged(nameof(ProfileName));
        OnPropertyChanged(nameof(EncodingName));
        OnPropertyChanged(nameof(EncodingWarning));
        RefreshView();
        ColumnsChanged?.Invoke();
    }

    private enum CopyMode { Tsv, Raw, LineRefs }

    private void Copy(CopyMode mode)
    {
        var selected = SelectedRows.Count > 0 ? SelectedRows : Rows.ToList();
        if (selected.Count == 0) return;

        var parsed = selected.Select(RowToParsed).ToList();
        string text = mode switch
        {
            CopyMode.Raw => ClipboardFormatter.RawText(parsed),
            CopyMode.LineRefs => ClipboardFormatter.LineReferences(parsed),
            _ => ClipboardFormatter.RowsAsTsv(parsed, Columns),
        };

        try { Clipboard.SetText(text); } catch { /* clipboard can be transiently locked */ }
    }

    // ---- Synchronized comparison (UR-13) ----
    private bool _isSyncEnabled = true;
    public bool IsSyncEnabled { get => _isSyncEnabled; set => SetField(ref _isSyncEnabled, value); }

    /// <summary>Highest physical line number currently loaded.</summary>
    public int LastLineNumber => _liveRows.Count > 0 ? _liveRows[^1].LineNumber : 0;

    /// <summary>(line, timestamp) pairs from the Timestamp-typed field, for timestamp sync.</summary>
    public IReadOnlyList<(int Line, DateTime Timestamp)> TimestampedRows()
    {
        var field = _document.Profile.TimestampField;
        if (field == null) return [];

        var list = new List<(int, DateTime)>();
        foreach (var row in _liveRows)
        {
            if (row.Fields.TryGetValue(field, out var value) &&
                Core.Sync.TimestampParser.TryParse(value) is { } ts)
                list.Add((row.LineNumber, ts));
        }
        return list;
    }

    public bool HasTimestampField => _document.Profile.TimestampField != null;

    /// <summary>Raised when a synced selection should be scrolled into view.</summary>
    public event Action<LogRowViewModel>? ScrollToRowRequested;

    /// <summary>Selects (and scrolls to) the row for the given physical line, or the nearest visible row.</summary>
    public void SelectLine(int line)
    {
        if (Rows.Count == 0) return;
        var match = Rows.FirstOrDefault(r => r.LineNumber == line)
                    ?? Rows.OrderBy(r => Math.Abs(r.LineNumber - line)).First();
        SelectedRow = match;
        ScrollToRowRequested?.Invoke(match);
    }

    // ---- View mode ----
    private bool _isRawView;
    public bool IsRawView
    {
        get => _isRawView;
        set { if (SetField(ref _isRawView, value)) { OnPropertyChanged(nameof(IsTableView)); } }
    }
    public bool IsTableView => !_isRawView;

    public string RawText => string.Join(Environment.NewLine, _liveRaw.Select(l => l.Text));

    // ---- Filtering ----
    private string _filterRegexError = string.Empty;
    public string FilterRegexError { get => _filterRegexError; private set => SetField(ref _filterRegexError, value); }
    public bool HasFilterRegexError => !string.IsNullOrEmpty(_filterRegexError);

    private string _searchRegexError = string.Empty;
    public string SearchRegexError { get => _searchRegexError; private set => SetField(ref _searchRegexError, value); }
    public bool HasSearchRegexError => !string.IsNullOrEmpty(_searchRegexError);

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set { if (SetField(ref _filterText, value)) RefreshView(); }
    }

    private bool _filterIsRegex;
    public bool FilterIsRegex
    {
        get => _filterIsRegex;
        set { if (SetField(ref _filterIsRegex, value)) RefreshView(); }
    }

    private bool _onlyFlagged;
    public bool OnlyFlagged
    {
        get => _onlyFlagged;
        set { if (SetField(ref _onlyFlagged, value)) RefreshView(); }
    }

    // ---- Exclude filter (UR-08) ----
    private string _excludeText = string.Empty;
    public string ExcludeText
    {
        get => _excludeText;
        set { if (SetField(ref _excludeText, value)) RefreshView(); }
    }

    private bool _excludeIsRegex;
    public bool ExcludeIsRegex
    {
        get => _excludeIsRegex;
        set { if (SetField(ref _excludeIsRegex, value)) RefreshView(); }
    }

    // ---- Field-scoped filter (UR-08): limit include to a specific column ----
    private string _filterField = string.Empty;
    public string FilterField
    {
        get => _filterField;
        set { if (SetField(ref _filterField, value)) RefreshView(); }
    }

    // ---- Time-range filter (UR-08, shown when a Timestamp field exists) ----
    private string _filterTimeFrom = string.Empty;
    public string FilterTimeFrom
    {
        get => _filterTimeFrom;
        set { if (SetField(ref _filterTimeFrom, value)) RefreshView(); }
    }

    private string _filterTimeTo = string.Empty;
    public string FilterTimeTo
    {
        get => _filterTimeTo;
        set { if (SetField(ref _filterTimeTo, value)) RefreshView(); }
    }

    // ---- Search ----
    private string _searchText = string.Empty;
    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }

    private bool _searchCaseSensitive;
    public bool SearchCaseSensitive { get => _searchCaseSensitive; set => SetField(ref _searchCaseSensitive, value); }

    private bool _searchIsRegex;
    public bool SearchIsRegex { get => _searchIsRegex; set => SetField(ref _searchIsRegex, value); }

    private bool _searchWholeWord;
    public bool SearchWholeWord { get => _searchWholeWord; set => SetField(ref _searchWholeWord, value); }

    /// <summary>Search scope: "All fields" or a specific column (UR-08).</summary>
    public ObservableCollection<string> SearchFields { get; } = [];
    private const string AllFields = "All fields";

    private string _searchField = AllFields;
    public string SearchField { get => _searchField; set => SetField(ref _searchField, value); }

    private LogRowViewModel? _selectedRow;
    public LogRowViewModel? SelectedRow { get => _selectedRow; set => SetField(ref _selectedRow, value); }

    public RelayCommand FindNextCommand { get; }
    public RelayCommand FindPrevCommand { get; }

    // ---- Status ----
    private string _status = string.Empty;
    public string Status { get => _status; private set => SetField(ref _status, value); }

    private int _flaggedCount;
    public int FlaggedCount { get => _flaggedCount; private set => SetField(ref _flaggedCount, value); }

    // ---- Streaming ----
    private bool _streamingEnabled;
    public bool StreamingEnabled
    {
        get => _streamingEnabled;
        set
        {
            if (!SetField(ref _streamingEnabled, value)) return;
            if (value) StartStreaming();
            else StopStreaming();
        }
    }

    /// <summary>When true, new streamed entries auto-scroll into view (UR-12 follow newest).</summary>
    private bool _autoFollow = true;
    public bool AutoFollow
    {
        get => _autoFollow;
        set
        {
            if (!SetField(ref _autoFollow, value)) return;
            if (value) NewEntriesCount = 0;
            OnPropertyChanged(nameof(ShowNewEntriesBadge));
        }
    }

    /// <summary>Count of entries appended while the user has scrolled away from the tail.</summary>
    private int _newEntriesCount;
    public int NewEntriesCount
    {
        get => _newEntriesCount;
        private set { if (SetField(ref _newEntriesCount, value)) OnPropertyChanged(nameof(ShowNewEntriesBadge)); }
    }

    public bool ShowNewEntriesBadge => StreamingEnabled && !AutoFollow && NewEntriesCount > 0;

    /// <summary>Raised when the view should scroll to the newest row (live-follow).</summary>
    public event Action? ScrollToEndRequested;

    public RelayCommand ReturnToLiveCommand => _returnToLive ??= new RelayCommand(() =>
    {
        AutoFollow = true;
        ScrollToEndRequested?.Invoke();
    });
    private RelayCommand? _returnToLive;

    // ---------------------------------------------------------------

    private void RefreshView()
    {
        var flagResult = _flagEngine.Evaluate(_liveRows);
        var flaggedSet = new HashSet<int>(flagResult.FlaggedLineNumbers);
        FlaggedCount = flagResult.FlaggedCount;

        IEnumerable<ParsedRow> rows = FilterRows(_liveRows, flaggedSet);

        // Continuation lines keyed by the primary row's line number
        var continuationByLine = _document.Events
            .Where(e => e.ContinuationLines.Count > 0)
            .ToDictionary(e => e.PrimaryRow.LineNumber, e => e.ContinuationLines);

        Rows.Clear();
        foreach (var row in rows)
        {
            var styling = _colorEngine.Evaluate(row);
            var continuation = continuationByLine.GetValueOrDefault(row.LineNumber, Array.Empty<RawLogLine>());
            Rows.Add(new LogRowViewModel(row, continuation, styling.RowBackground, flaggedSet.Contains(row.LineNumber)));
        }

        UpdateStatus();
    }

    /// <summary>Applies the current filter (text/regex + flagged-only) to a row set.</summary>
    private IEnumerable<ParsedRow> FilterRows(IEnumerable<ParsedRow> rows, ISet<int> flaggedSet)
    {
        FilterRegexError = string.Empty;

        var rules = new List<FilterRule>();
        var fieldScope = string.IsNullOrEmpty(FilterField) || FilterField == "All fields" ? null : FilterField;

        if (!string.IsNullOrEmpty(FilterText))
        {
            rules.Add(FilterIsRegex
                ? FilterRule.IncludeMatchingRegex(FilterText, fieldScope)
                : FilterRule.IncludeContainingText(FilterText, caseSensitive: false, fieldScope));
        }

        if (!string.IsNullOrEmpty(ExcludeText))
        {
            rules.Add(ExcludeIsRegex
                ? FilterRule.ExcludeMatchingRegex(ExcludeText)
                : FilterRule.ExcludeContainingText(ExcludeText, caseSensitive: false));
        }

        if (rules.Count > 0)
        {
            try
            {
                rows = new FilterEngine(rules).Apply(rows);
            }
            catch (System.Text.RegularExpressions.RegexParseException ex)
            {
                FilterRegexError = ex.Message;
                OnPropertyChanged(nameof(HasFilterRegexError));
                return rows.Where(r => !OnlyFlagged || flaggedSet.Contains(r.LineNumber));
            }
        }

        OnPropertyChanged(nameof(HasFilterRegexError));

        if (OnlyFlagged)
            rows = rows.Where(r => flaggedSet.Contains(r.LineNumber));

        // Time-range filter (UR-08) when a Timestamp field is mapped.
        var field = _document.Profile.TimestampField;
        if (field != null)
        {
            var from = Core.Sync.TimestampParser.TryParse(FilterTimeFrom);
            var to = Core.Sync.TimestampParser.TryParse(FilterTimeTo);
            if (from != null || to != null)
                rows = TimeRangeFilter.Apply(rows, field, from, to);
        }

        return rows;
    }

    private void UpdateStatus()
    {
        var parts = new List<string>
        {
            $"{Rows.Count} of {_liveRows.Count} rows",
            $"parsed {_parsedCount}, fallback {_fallbackCount}",
            $"{FlaggedCount} flagged",
            $"profile: {ProfileName}",
            _document.EncodingName,
        };
        if (_document.Truncated)
            parts.Add($"⚠ showing first {_document.Rows.Count:N0} lines (file is larger)");
        if (!string.IsNullOrEmpty(_document.EncodingWarning))
            parts.Add("⚠ " + _document.EncodingWarning);

        Status = string.Join("  •  ", parts);
    }

    private void FindNext()
    {
        if (string.IsNullOrEmpty(SearchText) || Rows.Count == 0) return;
        int start = SelectedRow != null ? Rows.IndexOf(SelectedRow) + 1 : 0;
        FindFrom(start, forward: true);
    }

    private void FindPrev()
    {
        if (string.IsNullOrEmpty(SearchText) || Rows.Count == 0) return;
        int start = SelectedRow != null ? Rows.IndexOf(SelectedRow) - 1 : Rows.Count - 1;
        FindFrom(start, forward: false);
    }

    private void FindFrom(int start, bool forward)
    {
        SearchRegexError = string.Empty;
        OnPropertyChanged(nameof(HasSearchRegexError));

        if (SearchIsRegex)
        {
            try { _ = new System.Text.RegularExpressions.Regex(SearchText); }
            catch (System.Text.RegularExpressions.RegexParseException ex)
            {
                SearchRegexError = ex.Message;
                OnPropertyChanged(nameof(HasSearchRegexError));
                return;
            }
        }

        var field = SearchField == AllFields ? null : SearchField;
        var query = new SearchQuery(SearchText, SearchCaseSensitive, SearchIsRegex, SearchWholeWord, field);

        int n = Rows.Count;
        for (int i = 0; i < n; i++)
        {
            int idx = forward ? (start + i) % n : ((start - i) % n + n) % n;
            var row = Rows[idx];
            var matches = _searchEngine.Search([RowToParsed(row)], query);
            if (matches.Any())
            {
                SelectedRow = row;
                return;
            }
        }
    }

    private static ParsedRow RowToParsed(LogRowViewModel vm)
    {
        var dict = vm.Fields.ToDictionary(kv => kv.Key, kv => kv.Value);
        return new ParsedRow(vm.LineNumber, dict);
    }

    private void StartStreaming()
    {
        AutoFollow = true;
        _watcher = new LogStreamWatcher(FilePath);
        _watcher.NewLinesAvailable += OnNewLines;
        // Resume after the content already loaded so nothing appended before now is skipped.
        _ = _watcher.StartAsync(LastLineNumber);
    }

    private void StopStreaming()
    {
        if (_watcher == null) return;
        _watcher.NewLinesAvailable -= OnNewLines;
        _watcher.Stop();
        _watcher.Dispose();
        _watcher = null;
    }

    /// <summary>
    /// Appends only the newly read lines (no full reload), applying the active profile,
    /// filter, and color/flag rules, then follows the tail when AutoFollow is on (UR-12).
    /// </summary>
    private void OnNewLines(IReadOnlyList<RawLogLine> newRaw)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        dispatcher.BeginInvoke(() =>
        {
            if (!StreamingEnabled) return;

            var (parsedRows, parsed, fallback) = LogDocument.ParseLines(newRaw, _document.Profile);
            _liveRaw.AddRange(newRaw);
            _liveRows.AddRange(parsedRows);
            _parsedCount += parsed;
            _fallbackCount += fallback;

            var flagged = _flagEngine.Evaluate(parsedRows);
            var flaggedSet = new HashSet<int>(flagged.FlaggedLineNumbers);
            FlaggedCount += flagged.FlaggedCount;

            int appendedToView = 0;
            foreach (var row in FilterRows(parsedRows, flaggedSet))
            {
                var styling = _colorEngine.Evaluate(row);
                Rows.Add(new LogRowViewModel(row, Array.Empty<RawLogLine>(), styling.RowBackground,
                    flaggedSet.Contains(row.LineNumber)));
                appendedToView++;
            }

            OnPropertyChanged(nameof(RawText));
            UpdateStatus();

            if (appendedToView > 0)
            {
                if (AutoFollow)
                    ScrollToEndRequested?.Invoke();
                else
                    NewEntriesCount += appendedToView;
            }
        });
    }

    public void Dispose() => StopStreaming();
}
