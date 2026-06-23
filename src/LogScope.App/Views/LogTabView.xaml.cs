using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LogScope.App.ViewModels;

namespace LogScope.App.Views;

public partial class LogTabView : UserControl
{
    public LogTabView()
    {
        InitializeComponent();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is LogTabViewModel vm)
            GenerateColumns(vm);
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
        });

        foreach (var column in vm.Columns)
        {
            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = column,
                // Bind to the row's dictionary entry; missing keys render empty.
                Binding = new Binding($"Fields[{column}]") { FallbackValue = string.Empty, TargetNullValue = string.Empty },
                Width = column == "Message" || column == "RawText"
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : DataGridLength.Auto,
            });
        }
    }
}
