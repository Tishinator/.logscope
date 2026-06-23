using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using LogScope.App.ViewModels;

namespace LogScope.App.Views;

public partial class LogTabView : UserControl
{
    private LogTabViewModel? _vm;

    public LogTabView()
    {
        InitializeComponent();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.RestoreOrderRequested -= RestoreOrder;

        if (e.NewValue is LogTabViewModel vm)
        {
            _vm = vm;
            _vm.RestoreOrderRequested += RestoreOrder;
            GenerateColumns(vm);
        }
    }

    /// <summary>
    /// The table's columns are data-driven (they depend on the parser profile), so we
    /// build them in code rather than XAML. A leading Line # column is always present.
    /// </summary>
    private void GenerateColumns(LogTabViewModel vm)
    {
        Grid.Columns.Clear();

        Grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Line",
            Binding = new Binding(nameof(LogRowViewModel.LineNumber)),
            Width = new DataGridLength(60),
            IsReadOnly = true,
            SortMemberPath = nameof(LogRowViewModel.LineNumber),
        });

        foreach (var column in vm.Columns)
        {
            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = column,
                Binding = new Binding($"Fields[{column}]") { FallbackValue = string.Empty, TargetNullValue = string.Empty },
                Width = column == "Message" || column == "RawText"
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : DataGridLength.Auto,
            });
        }
    }

    /// <summary>UR-09: restore original file order by clearing any column sort.</summary>
    private void RestoreOrder()
    {
        Grid.Items.SortDescriptions.Clear();
        foreach (var col in Grid.Columns)
            col.SortDirection = null;
        Grid.Items.Refresh();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm != null)
            _vm.SelectedRows = Grid.SelectedItems.Cast<LogRowViewModel>().ToList();
    }

    private void OnCopyMenu(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }
}
