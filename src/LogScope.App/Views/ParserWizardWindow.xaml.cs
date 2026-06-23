using System.Text;
using System.Windows;
using LogScope.Core.Documents;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.App.Views;

public partial class ParserWizardWindow : Window
{
    private readonly IReadOnlyList<RawLogLine> _sample;

    public LogProfile? ResultProfile { get; private set; }
    public bool SaveToLibrary => SaveToLibraryBox.IsChecked == true;

    public ParserWizardWindow(IReadOnlyList<string> sampleLines)
    {
        InitializeComponent();
        _sample = sampleLines.Select((t, i) => new RawLogLine(i + 1, t)).ToList();
        Loaded += (_, _) => OnPreview(this, new RoutedEventArgs());
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (DelimitedPanel == null) return; // not yet initialized
        DelimitedPanel.Visibility = DelimitedRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RegexPanel.Visibility = RegexRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
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
        ResultProfile = profile;
        DialogResult = true;
        Close();
    }
}
