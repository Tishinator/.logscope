using System.Collections.ObjectModel;
using System.Windows;
using LogScope.App.ViewModels;
using LogScope.Core.Persistence;
using LogScope.Core.Visualization;

namespace LogScope.App.Views;

public partial class ColorRulesWindow : Window
{
    private readonly ObservableCollection<ColorRuleEditViewModel> _rows;

    /// <summary>The edited rules (valid only when DialogResult is true).</summary>
    public List<ColorRuleDto> Rules { get; private set; } = [];

    public ColorRulesWindow(IEnumerable<ColorRuleDto> existing)
    {
        InitializeComponent();
        _rows = new ObservableCollection<ColorRuleEditViewModel>(existing.Select(d => new ColorRuleEditViewModel(d)));
        Grid.ItemsSource = _rows;
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var row = new ColorRuleEditViewModel
        {
            Kind = ColorRule.MatchKind.Regex,
            MatchValue = "TEXT",
            Background = "#2D4A5A",
            Priority = (_rows.Count == 0 ? 1 : _rows.Max(r => r.Priority) + 1),
        };
        _rows.Add(row);
        Grid.SelectedItem = row;
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is ColorRuleEditViewModel row)
            _rows.Remove(row);
    }

    private void OnMoveUp(object sender, RoutedEventArgs e) => Move(-1);
    private void OnMoveDown(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int delta)
    {
        if (Grid.SelectedItem is not ColorRuleEditViewModel row) return;
        int i = _rows.IndexOf(row);
        int j = i + delta;
        if (j < 0 || j >= _rows.Count) return;
        _rows.Move(i, j);
        Grid.SelectedItem = row;
    }

    private void OnRestoreDefaults(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        foreach (var d in DefaultRuleSets.ColorRules())
            _rows.Add(new ColorRuleEditViewModel(d));
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(); // flush any in-progress cell edit
        Rules = _rows
            .Where(r => !string.IsNullOrWhiteSpace(r.MatchValue))
            .Select(r => r.ToDto())
            .ToList();
        DialogResult = true;
        Close();
    }
}
