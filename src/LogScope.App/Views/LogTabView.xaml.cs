using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using LogScope.App.ViewModels;
using LogScope.Core.Documents;

namespace LogScope.App.Views;

/// <summary>Sorts the Level column by configured severity order rather than alphabetically (UR-06/UR-09).</summary>
internal sealed class LevelSeverityComparer(LogProfile profile, string levelField, ListSortDirection direction) : IComparer
{
    public int Compare(object? x, object? y)
    {
        var a = (x as LogRowViewModel)?.GetField(levelField) ?? string.Empty;
        var b = (y as LogRowViewModel)?.GetField(levelField) ?? string.Empty;
        int result = profile.LevelRank(a).CompareTo(profile.LevelRank(b));
        return direction == ListSortDirection.Ascending ? result : -result;
    }
}

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
        {
            _vm.RestoreOrderRequested -= RestoreOrder;
            _vm.ScrollToRowRequested -= ScrollToRow;
            _vm.ColumnsChanged -= OnColumnsChanged;
            _vm.ScrollToEndRequested -= ScrollToEnd;
        }

        if (e.NewValue is LogTabViewModel vm)
        {
            _vm = vm;
            _vm.RestoreOrderRequested += RestoreOrder;
            _vm.ScrollToRowRequested += ScrollToRow;
            _vm.ColumnsChanged += OnColumnsChanged;
            _vm.ScrollToEndRequested += ScrollToEnd;
            GenerateColumns(vm);
        }
    }

    /// <summary>Auto-follow the tail and detect when the user scrolls away (UR-12).</summary>
    private void OnGridScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_vm == null || !_vm.StreamingEnabled) return;

        bool atBottom = e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 2.0;

        if (e.ExtentHeightChange != 0)
        {
            // Content grew (new streamed rows). Stay pinned to the tail while following.
            if (_vm.AutoFollow)
                ScrollViewerOf(Grid)?.ScrollToBottom();
        }
        else if (e.ViewportHeightChange == 0 && e.VerticalChange != 0)
        {
            // A genuine user scroll: follow only while pinned to the bottom.
            _vm.AutoFollow = atBottom;
        }
    }

    private void ScrollToEnd()
    {
        if (Rows().LastOrDefault() is { } last)
            Dispatcher.InvokeAsync(() => Grid.ScrollIntoView(last));
    }

    private IReadOnlyList<LogRowViewModel> Rows() =>
        _vm?.Rows as IReadOnlyList<LogRowViewModel> ?? [];

    private static System.Windows.Controls.ScrollViewer? ScrollViewerOf(DependencyObject root)
    {
        if (root is System.Windows.Controls.ScrollViewer sv) return sv;
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = ScrollViewerOf(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Rebuild grid columns after a new parser profile is applied to this tab.</summary>
    private void OnColumnsChanged()
    {
        if (_vm != null) GenerateColumns(_vm);
    }

    /// <summary>Scrolls a synced selection into view (UR-13).</summary>
    private void ScrollToRow(LogRowViewModel row)
    {
        if (row != null)
            Dispatcher.InvokeAsync(() => Grid.ScrollIntoView(row));
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

    /// <summary>Use severity order for the Level field; let the grid sort everything else normally.</summary>
    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        var levelField = _vm?.CurrentProfile.LevelField;
        if (levelField == null || !string.Equals(e.Column.Header?.ToString(), levelField, StringComparison.OrdinalIgnoreCase))
            return;

        e.Handled = true;
        var direction = e.Column.SortDirection != ListSortDirection.Ascending
            ? ListSortDirection.Ascending : ListSortDirection.Descending;
        e.Column.SortDirection = direction;

        if (CollectionViewSource.GetDefaultView(Grid.ItemsSource) is ListCollectionView view)
        {
            view.CustomSort = new LevelSeverityComparer(_vm!.CurrentProfile, levelField, direction);
            view.Refresh();
        }
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
