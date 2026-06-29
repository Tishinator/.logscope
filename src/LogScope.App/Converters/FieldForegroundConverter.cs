using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogScope.App.ViewModels;

namespace LogScope.App.Converters;

/// <summary>
/// Returns a Brush for the named field's foreground color from a LogRowViewModel.
/// ConverterParameter = field/column name.
/// Falls back to the dark-theme default (#DDD) so cell text is always legible
/// on the dark background when no per-field color rule applies.
/// </summary>
public sealed class FieldForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush DefaultForeground =
        new(Color.FromRgb(0xDD, 0xDD, 0xDD)); // #DDD — matches CellStyle default

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogRowViewModel row && parameter is string fieldName)
        {
            var hex = row.GetFieldForeground(fieldName);
            if (!string.IsNullOrEmpty(hex))
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { /* fall through to default */ }
            }
        }
        return DefaultForeground;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
