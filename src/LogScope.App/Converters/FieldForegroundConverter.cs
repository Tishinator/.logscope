using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogScope.App.ViewModels;

namespace LogScope.App.Converters;

/// <summary>
/// Returns a Brush for the named field's foreground color from a LogRowViewModel.
/// ConverterParameter = field/column name.
/// </summary>
public sealed class FieldForegroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogRowViewModel row && parameter is string fieldName)
        {
            var hex = row.GetFieldForeground(fieldName);
            if (!string.IsNullOrEmpty(hex))
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { /* fall through */ }
            }
        }
        return null; // let the cell inherit default foreground
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
