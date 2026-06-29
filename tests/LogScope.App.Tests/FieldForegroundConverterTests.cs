using System.Globalization;
using System.Windows.Media;
using FluentAssertions;
using LogScope.App.Converters;
using LogScope.App.ViewModels;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.App.Tests;

/// <summary>
/// Regression tests for FieldForegroundConverter.
/// The converter must never return null — returning null would set TextBlock.Foreground=null
/// making text invisible on the dark background (issue #34 root cause).
/// When no field override applies the converter returns the dark-theme default (#DDD)
/// so that WPF Style setters provide an explicit value rather than falling back to the
/// system-theme black (issue #37 root cause).
/// </summary>
public class FieldForegroundConverterTests
{
    private readonly FieldForegroundConverter _converter = new();

    private static LogRowViewModel MakeRow(string field, string value)
    {
        var row = new ParsedRow(1, new Dictionary<string, string> { [field] = value });
        return new LogRowViewModel(row, [], rowBackground: null, isFlagged: false, fieldOverrides: null);
    }

    [Fact]
    public void Returns_DefaultBrush_WhenNoOverrideForField()
    {
        var row = MakeRow("Level", "INFO");

        var result = _converter.Convert(row, typeof(object), "Level", CultureInfo.InvariantCulture);

        result.Should().BeOfType<SolidColorBrush>("converter must return an explicit brush so text is visible on the dark background");
        ((SolidColorBrush)result!).Color.Should().Be(Color.FromRgb(0xDD, 0xDD, 0xDD));
    }

    [Fact]
    public void Returns_DefaultBrush_WhenValueIsNull()
    {
        var result = _converter.Convert(null, typeof(object), "Level", CultureInfo.InvariantCulture);

        result.Should().BeOfType<SolidColorBrush>();
    }

    [Fact]
    public void Returns_DefaultBrush_WhenParameterIsNull()
    {
        var row = MakeRow("Level", "INFO");

        var result = _converter.Convert(row, typeof(object), null, CultureInfo.InvariantCulture);

        result.Should().BeOfType<SolidColorBrush>();
    }

    [Fact]
    public void Returns_Null_Never()
    {
        var result = _converter.Convert(null, typeof(object), null, CultureInfo.InvariantCulture);

        result.Should().NotBeNull("returning null from a Style setter binding makes text invisible");
    }
}
