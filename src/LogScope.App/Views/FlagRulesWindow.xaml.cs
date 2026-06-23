using System.Collections.ObjectModel;
using System.Windows;
using LogScope.App.ViewModels;
using LogScope.Core.Persistence;

namespace LogScope.App.Views;

public partial class FlagRulesWindow : Window
{
    private readonly ObservableCollection<FlagRuleEditViewModel> _rows;

    public List<FlagRuleDto> Rules { get; private set; } = [];

    public FlagRulesWindow(IEnumerable<FlagRuleDto> existing)
    {
        InitializeComponent();
        _rows = new ObservableCollection<FlagRuleEditViewModel>(existing.Select(d => new FlagRuleEditViewModel(d)));
        Grid.ItemsSource = _rows;
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var row = new FlagRuleEditViewModel { MatchValue = "ERROR" };
        _rows.Add(row);
        Grid.SelectedItem = row;
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is FlagRuleEditViewModel row)
            _rows.Remove(row);
    }

    private void OnRestoreDefaults(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        foreach (var d in DefaultRuleSets.FlagRules())
            _rows.Add(new FlagRuleEditViewModel(d));
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit();
        Rules = _rows.Where(r => !string.IsNullOrWhiteSpace(r.MatchValue)).Select(r => r.ToDto()).ToList();
        DialogResult = true;
        Close();
    }
}
