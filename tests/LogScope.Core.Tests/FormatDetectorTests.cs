using FluentAssertions;
using LogScope.Core.Parsing;

namespace LogScope.Core.Tests;

public class FormatDetectorTests
{
    [Fact]
    public void Detect_PipeDelimited_SuggestsDelimiterProfile()
    {
        var sample = new[]
        {
            "2024-01-15 10:23:45|ERROR|Auth|Login failed",
            "2024-01-15 10:23:46|INFO|Core|Started",
            "2024-01-15 10:23:47|WARN|Net|Retry",
        };

        var detector = new FormatDetector();
        var suggestion = detector.Detect(sample);

        suggestion.Kind.Should().Be(DetectedFormatKind.Delimited);
        suggestion.Delimiter.Should().Be("|");
        suggestion.FieldCount.Should().Be(4);
    }

    [Fact]
    public void Detect_DoublePipeDelimited_SuggestsMultiCharDelimiter()
    {
        var sample = new[]
        {
            "a||b||c",
            "d||e||f",
        };

        var detector = new FormatDetector();
        var suggestion = detector.Detect(sample);

        suggestion.Kind.Should().Be(DetectedFormatKind.Delimited);
        suggestion.Delimiter.Should().Be("||");
        suggestion.FieldCount.Should().Be(3);
    }

    [Fact]
    public void Detect_CommaDelimited_SuggestsComma()
    {
        var sample = new[]
        {
            "2024-01-15,ERROR,Auth,Login failed",
            "2024-01-15,INFO,Core,Started",
        };

        var detector = new FormatDetector();
        var suggestion = detector.Detect(sample);

        suggestion.Delimiter.Should().Be(",");
    }

    [Fact]
    public void Detect_TabDelimited_SuggestsTab()
    {
        var sample = new[]
        {
            "col1\tcol2\tcol3",
            "a\tb\tc",
        };

        var detector = new FormatDetector();
        var suggestion = detector.Detect(sample);

        suggestion.Delimiter.Should().Be("\t");
    }

    [Fact]
    public void Detect_NoConsistentDelimiter_SuggestsRawFallback()
    {
        var sample = new[]
        {
            "just some free-form text here",
            "another line with no structure at all",
            "third irregular entry",
        };

        var detector = new FormatDetector();
        var suggestion = detector.Detect(sample);

        suggestion.Kind.Should().Be(DetectedFormatKind.Raw);
    }

    [Fact]
    public void Detect_EmptySample_SuggestsRawFallback()
    {
        var detector = new FormatDetector();
        var suggestion = detector.Detect([]);

        suggestion.Kind.Should().Be(DetectedFormatKind.Raw);
    }

    [Fact]
    public void Detect_PrefersDelimiterPresentInAllLines()
    {
        // Comma appears in only one line, pipe appears in all → choose pipe
        var sample = new[]
        {
            "a|b|c",
            "d|e|f, with a stray comma",
            "g|h|i",
        };

        var detector = new FormatDetector();
        var suggestion = detector.Detect(sample);

        suggestion.Delimiter.Should().Be("|");
    }

    [Fact]
    public void Detect_GeneratedProfile_ProducesUsableFieldNames()
    {
        var sample = new[] { "a|b|c" };

        var detector = new FormatDetector();
        var suggestion = detector.Detect(sample);

        suggestion.SuggestedFieldNames.Should().HaveCount(3);
        suggestion.SuggestedFieldNames.Should().OnlyContain(n => !string.IsNullOrWhiteSpace(n));
    }
}
