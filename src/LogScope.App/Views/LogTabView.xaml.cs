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
            _vm.ColumnVisibilityChanged -= OnColumnVisibilityChanged;
            Grid.ColumnReordered -= OnColumnReordered;
        }

        if (e.NewValue is LogTabViewModel vm)
        {
            _vm = vm;
            _vm.RestoreOrderRequested += RestoreOrder;
            _vm.ScrollToRowRequested += ScrollToRow;
            _vm.ColumnsChanged += OnColumnsChanged;
            _vm.ScrollToEndRequested += ScrollToEnd;
            _vm.ColumnVisibilityChanged += OnColumnVisibilityChanged;
            Grid.ColumnReordered += OnColumnReordered;
            GenerateColumns(vm);
        }
    }

    /// <summary>Show/hide a single column without rebuilding the grid (SR-10).</summary>
    private void OnColumnVisibilityChanged(string name, bool visible)
    {
        foreach (var col in Grid.Columns)
        {
            if (Equals(col.Header, name))
            {
                col.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                break;
            }
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

        AddColumn(vm, "Line",
            new Binding(nameof(LogRowViewModel.LineNumber)), new DataGridLength(60));

        foreach (var column in vm.Columns)
        {
            var defaultWidth = column == "Message" || column == "RawText"
                ? new DataGridLength(1, DataGridLengthUnitType.Star)
                : DataGridLength.Auto;
            AddColumn(vm, column,
                new Binding($"Fields[{column}]") { FallbackValue = string.Empty, TargetNullValue = string.Empty },
                defaultWidth);
        }

        // Apply saved display-index order after all columns are added.
        var ordered = Grid.Columns
            .Where(c => c.Header is string h && vm.SavedColumnGeometry.ContainsKey(h))
            .OrderBy(c => vm.SavedColumnGeometry[c.Header.ToString()!].DisplayIndex)
            .ToList();
        foreach (var col in ordered)
        {
            int saved = vm.SavedColumnGeometry[col.Header.ToString()!].DisplayIndex;
            if (saved >= 0 && saved < Grid.Columns.Count)
                col.DisplayIndex = saved;
        }
    }

    private void AddColumn(LogTabViewModel vm, string name, Binding binding, DataGridLength defaultWidth)
    {
        var savedWidth = vm.SavedColumnGeometry.TryGetValue(name, out var geo) && geo.Width > 0
            ? new DataGridLength(geo.Width)
            : defaultWidth;

        var col = new DataGridTextColumn
        {
            Header = name,
            Binding = binding,
            Width = savedWidth,
            IsReadOnly = true,
            SortMemberPath = name == "Line" ? nameof(LogRowViewModel.LineNumber) : null,
            Visibility = vm.IsColumnVisible(name) ? Visibility.Visible : Visibility.Collapsed,
        };

        // Persist width when the user finishes resizing.
        DependencyPropertyDescriptor
            .FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn))
            .AddValueChanged(col, (_, _) =>
            {
                if (_vm != null && col.Header is string n)
                    _vm.ReportColumnState(n, col.ActualWidth, col.DisplayIndex);
            });

        Grid.Columns.Add(col);
    }

    private void OnColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (_vm == null || e.Column.Header is not string name) return;
        _vm.ReportColumnState(name, e.Column.ActualWidth, e.Column.DisplayIndex);
        // Also update all other columns' display indices since they shift.
        foreach (var col in Grid.Columns)
            if (col.Header is string n && n != name)
                _vm.ReportColumnState(n, col.ActualWidth, col.DisplayIndex);
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
