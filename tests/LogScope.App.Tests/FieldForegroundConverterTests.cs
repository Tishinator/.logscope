using System.Globalization;
using System.Windows;
using FluentAssertions;
using LogScope.App.Converters;
using LogScope.App.ViewModels;
using LogScope.Core.Documents;
using LogScope.Core.Parsing;
using LogScope.Core.Reading;

namespace LogScope.App.Tests;

/// <summary>
/// Regression tests for FieldForegroundConverter.
/// The converter must return DependencyProperty.UnsetValue (not null) when no field
/// foreground override applies — returning null sets a local Foreground=null on the
/// TextBlock which makes text invisible on the dark background (issue #34 root cause).
/// </summary>
public class FieldForegroundConverterTests
{
    private readonly FieldForegroundConverter _converter = new();

    private static LogRowViewModel MakeRow(string field, string value)
    {
        var raw = new RawLogLine(1, $"{field}={value}");
        var row = new ParsedRow(1, new Dictionary<string, string> { [field] = value });
        return new LogRowViewModel(row, [], rowBackground: null, isFlagged: false, fieldOverrides: null);
    }

    [Fact]
    public void Returns_UnsetValue_WhenNoOverrideForField()
    {
        var row = MakeRow("Level", "INFO");

        var result = _converter.Convert(row, typeof(object), "Level", CultureInfo.InvariantCulture);

        result.Should().Be(DependencyProperty.UnsetValue,
            "returning null would set TextBlock.Foreground=null, making text invisible on the dark background");
    }

    [Fact]
    public void Returns_UnsetValue_WhenValueIsNull()
    {
        var result = _converter.Convert(null, typeof(object), "Level", CultureInfo.InvariantCulture);

        result.Should().Be(DependencyProperty.UnsetValue);
    }

    [Fact]
    public void Returns_UnsetValue_WhenParameterIsNull()
    {
        var row = MakeRow("Level", "INFO");

        var result = _converter.Convert(row, typeof(object), null, CultureInfo.InvariantCulture);

        result.Should().Be(DependencyProperty.UnsetValue);
    }
}
