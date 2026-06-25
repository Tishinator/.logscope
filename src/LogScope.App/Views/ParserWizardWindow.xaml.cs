using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using LogScope.Core.Documents;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.App.Views;

/// <summary>Editable field → semantic-type row for the wizard (UR-06).</summary>
public sealed class FieldTypeRow : INotifyPropertyChanged
{
    public string Name { get; init; } = string.Empty;
    private FieldSemanticType _type;
    public FieldSemanticType Type
    {
        get => _type;
        set { _type = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class ParserWizardWindow : Window
{
    private readonly IReadOnlyList<RawLogLine> _sample;
    private readonly ObservableCollection<FieldTypeRow> _fieldTypes = [];

    public static IReadOnlyList<FieldSemanticType> FieldTypeValues { get; } =
        Enum.GetValues<FieldSemanticType>();

    public LogProfile? ResultProfile { get; private set; }
    public bool SaveToLibrary => SaveToLibraryBox.IsChecked == true;

    public ParserWizardWindow(IReadOnlyList<string> sampleLines)
    {
        InitializeComponent();
        _sample = sampleLines.Select((t, i) => new RawLogLine(i + 1, t)).ToList();
        FieldTypesGrid.ItemsSource = _fieldTypes;
        Loaded += (_, _) => OnPreview(this, new RoutedEventArgs());
    }

    private void SyncFieldTypeRows(IEnumerable<string> fieldNames)
    {
        var existing = _fieldTypes.ToDictionary(r => r.Name, r => r.Type);
        _fieldTypes.Clear();
        foreach (var name in fieldNames)
        {
            if (name == "RawText") continue;
            var type = existing.TryGetValue(name, out var t) ? t : FieldSemanticGuesser.Guess(name);
            _fieldTypes.Add(new FieldTypeRow { Name = name, Type = type });
        }
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (DelimitedPanel == null) return; // not yet initialized
        DelimitedPanel.Visibility = DelimitedRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RegexPanel.Visibility = RegexRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFieldsTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (FieldsBox == null) return;
        var fields = FieldsBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(f => f != "RawText")
            .ToList();
        if (fields.Count > 0)
            SyncFieldTypeRows(fields);
    }

    private void OnPatternTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (PatternBox == null) return;
        try
        {
            var groups = System.Text.RegularExpressions.Regex
                .Matches(PatternBox.Text, @"\(\?<(\w+)>")
                .Select(m => m.Groups[1].Value)
                .ToList();
            if (groups.Count > 0)
                SyncFieldTypeRows(groups);
        }
        catch { /* ignore malformed pattern during editing */ }
    }

    private void OnQuickDelimiter(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
            DelimiterBox.Text = tag;
    }

    private LogProfile? BuildProfile()
    {
        LogProfile profile;
        try
        {
            if (RegexRadio.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(PatternBox.Text)) return null;
                profile = LogProfile.Regex(PatternBox.Text);
            }
            else if (RawRadio.IsChecked == true)
            {
                profile = LogProfile.Raw();
            }
            else
            {
                var fields = FieldsBox.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                if (fields.Count == 0 || string.IsNullOrEmpty(DelimiterBox.Text)) return null;
                profile = LogProfile.Delimited(DelimiterBox.Text, fields);
            }
        }
        catch
        {
            return null;
        }

        profile.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "My profile" : NameBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(MultilineBox.Text))
            profile.WithMultiline(MultilineBox.Text.Trim());

        // Apply field semantic types (UR-06)
        foreach (var row in _fieldTypes)
            if (row.Type != FieldSemanticType.Generic)
                profile.SetFieldType(row.Name, row.Type);

        // Apply custom level order
        var levels = LevelOrderBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (levels.Count > 0)
            profile.LevelOrder = levels;

        return profile;
    }

    private void OnPreview(object sender, RoutedEventArgs e)
    {
        var profile = BuildProfile();
        if (profile == null)
        {
            StatsText.Text = "Configuration incomplete or invalid.";
            PreviewBox.Text = string.Empty;
            return;
        }

        try
        {
            var (rows, parsed, fallback) = ParsePreview(profile);
            var sb = new StringBuilder();
            foreach (var row in rows.Take(25))
            {
                sb.Append('#').Append(row.LineNumber).Append("  ");
                sb.AppendLine(string.Join("  |  ", row.Fields.Select(f => $"{f.Key}={f.Value}")));
            }
            PreviewBox.Text = sb.ToString();
            StatsText.Text = $"Parsed {parsed}, fallback {fallback} of {_sample.Count} sample lines.";

            // Refresh the field-type rows from the columns this profile produces.
            var columns = rows.SelectMany(r => r.Fields.Keys).Distinct().ToList();
            SyncFieldTypeRows(columns);
        }
        catch (Exception ex)
        {
            StatsText.Text = "Error: " + ex.Message;
            PreviewBox.Text = string.Empty;
        }
    }

    private (IReadOnlyList<ParsedRow> rows, int parsed, int fallback) ParsePreview(LogProfile profile)
    {
        switch (profile.Kind)
        {
            case LogProfileKind.Delimited:
                var dp = new DelimiterParser(new DelimiterProfile(profile.Delimiter!, profile.FieldNames.ToArray()));
                var dr = dp.Parse(_sample).ToList();
                return (dr, dp.ParsedCount, dp.FallbackCount);
            case LogProfileKind.Regex:
                var rp = new RegexParser(new RegexProfile(profile.Pattern!));
                var rr = rp.Parse(_sample).ToList();
                return (rr, rp.ParsedCount, rp.FallbackCount);
            default:
                var fp = new RawFallbackParser();
                var fr = fp.Parse(_sample).ToList();
                return (fr, fp.ParsedCount, fp.FallbackCount);
        }
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        var profile = BuildProfile();
        if (profile == null)
        {
            MessageBox.Show("The parser configuration is incomplete or invalid.", "Parser setup",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var error = ValidateProfile(profile);
        if (error != null)
        {
            MessageBox.Show(error, "Parser setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultProfile = profile;
        DialogResult = true;
        Close();
    }

    private static string? ValidateProfile(LogProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
            return "Profile name must not be blank.";

        if (profile.Kind == LogProfileKind.Delimited)
        {
            if (string.IsNullOrEmpty(profile.Delimiter))
                return "Delimiter must not be empty.";

            if (profile.FieldNames.Count == 0)
                return "At least one field name is required.";

            var duplicates = profile.FieldNames
                .GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                return $"Duplicate field names: {string.Join(", ", duplicates)}";
        }

        if (profile.Kind == LogProfileKind.Regex)
        {
            if (string.IsNullOrWhiteSpace(profile.Pattern))
                return "Regex pattern must not be blank.";

            try { _ = new System.Text.RegularExpressions.Regex(profile.Pattern); }
            catch (System.Text.RegularExpressions.RegexParseException ex)
                { return $"Regex pattern is invalid: {ex.Message}"; }

            var hasNamedGroups = System.Text.RegularExpressions.Regex.IsMatch(profile.Pattern, @"\(\?<\w+>");
            if (!hasNamedGroups)
                return "Regex pattern must contain at least one named capture group, e.g. (?<Level>\\w+).";
        }

        return null;
    }
}
