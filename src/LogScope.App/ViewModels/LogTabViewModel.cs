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

    public string Title { get; }
    public string FilePath { get; }
    public IReadOnlyList<string> Columns { get; private set; }
    public ObservableCollection<LogRowViewModel> Rows { get; } = [];

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

    /// <summary>Replaces the underlying document (e.g. after applying a new parser profile).</summary>
    public void ApplyDocument(LogDocument document)
    {
        _document = document;
        Columns = document.Columns;
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(RawText));
        OnPropertyChanged(nameof(ProfileName));
        OnPropertyChanged(nameof(EncodingName));
        OnPropertyChanged(nameof(EncodingWarning));
        RefreshView();
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

    // ---- View mode ----
    private bool _isRawView;
    public bool IsRawView
    {
        get => _isRawView;
        set { if (SetField(ref _isRawView, value)) { OnPropertyChanged(nameof(IsTableView)); } }
    }
    public bool IsTableView => !_isRawView;

    public string RawText => string.Join(Environment.NewLine, _document.RawLines.Select(l => l.Text));

    // ---- Filtering ----
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

    // ---- Search ----
    private string _searchText = string.Empty;
    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }

    private bool _searchCaseSensitive;
    public bool SearchCaseSensitive { get => _searchCaseSensitive; set => SetField(ref _searchCaseSensitive, value); }

    private bool _searchIsRegex;
    public bool SearchIsRegex { get => _searchIsRegex; set => SetField(ref _searchIsRegex, value); }

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

    // ---------------------------------------------------------------

    private void RefreshView()
    {
        var flagResult = _flagEngine.Evaluate(_document.Rows);
        var flaggedSet = new HashSet<int>(flagResult.FlaggedLineNumbers);
        FlaggedCount = flagResult.FlaggedCount;

        IEnumerable<ParsedRow> rows = _document.Rows;

        if (!string.IsNullOrEmpty(FilterText))
        {
            var rule = FilterIsRegex
                ? FilterRule.IncludeMatchingRegex(FilterText)
                : FilterRule.IncludeContainingText(FilterText, caseSensitive: false);
            rows = new FilterEngine([rule]).Apply(rows);
        }

        if (OnlyFlagged)
            rows = rows.Where(r => flaggedSet.Contains(r.LineNumber));

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

        var parts = new List<string>
        {
            $"{Rows.Count} of {_document.Rows.Count} rows",
            $"parsed {_document.ParsedCount}, fallback {_document.FallbackCount}",
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
        var query = new SearchQuery(SearchText, SearchCaseSensitive, SearchIsRegex);
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
        _watcher = new LogStreamWatcher(FilePath);
        _watcher.NewLinesAvailable += OnNewLines;
        _ = _watcher.StartAsync();
        Status = "Streaming…  " + Status;
    }

    private void StopStreaming()
    {
        if (_watcher == null) return;
        _watcher.NewLinesAvailable -= OnNewLines;
        _watcher.Stop();
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnNewLines(IReadOnlyList<RawLogLine> _)
    {
        // Reload the document with the same profile to pick up appended content,
        // then refresh on the UI thread.
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _document = LogDocument.Load(FilePath, _document.Profile);
            Columns = _document.Columns;
            OnPropertyChanged(nameof(RawText));
            RefreshView();
        });
    }

    public void Dispose() => StopStreaming();
}
