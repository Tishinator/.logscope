using System.Text;
using FluentAssertions;
using LogScope.Core.Reading;

namespace LogScope.Core.Tests;

public class EncodingDetectorTests
{
    [Fact]
    public void Detect_Utf8Bom_IsDetectedWithBom()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i'];

        var result = EncodingDetector.Detect(bytes);

        result.Encoding.CodePage.Should().Be(Encoding.UTF8.CodePage);
        result.HadBom.Should().BeTrue();
        result.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void Detect_Utf16LeBom_IsDetected()
    {
        byte[] bytes = [0xFF, 0xFE, (byte)'h', 0x00];

        var result = EncodingDetector.Detect(bytes);

        result.Encoding.CodePage.Should().Be(Encoding.Unicode.CodePage);
        result.HadBom.Should().BeTrue();
    }

    [Fact]
    public void Detect_Utf16BeBom_IsDetected()
    {
        byte[] bytes = [0xFE, 0xFF, 0x00, (byte)'h'];

        var result = EncodingDetector.Detect(bytes);

        result.Encoding.CodePage.Should().Be(Encoding.BigEndianUnicode.CodePage);
        result.HadBom.Should().BeTrue();
    }

    [Fact]
    public void Detect_PlainAscii_IsUtf8WithoutBom_NotFallback()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("2024-01-15 INFO hello world");

        var result = EncodingDetector.Detect(bytes);

        result.Encoding.CodePage.Should().Be(Encoding.UTF8.CodePage);
        result.HadBom.Should().BeFalse();
        result.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void Detect_ValidUtf8WithoutBom_IsUtf8()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("café — naïve résumé");

        var result = EncodingDetector.Detect(bytes);

        result.Encoding.CodePage.Should().Be(Encoding.UTF8.CodePage);
        result.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void Detect_InvalidUtf8_FallsBackToAnsi_WithWarning()
    {
        // 0x93/0x94 are Windows-1252 smart quotes — invalid as standalone UTF-8.
        byte[] bytes = [(byte)'h', 0x93, (byte)'i', 0x94, (byte)'!'];

        var result = EncodingDetector.Detect(bytes);

        result.IsFallback.Should().BeTrue();
        result.Warning.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Detect_FallbackEncoding_DecodesAllBytesWithoutThrowing()
    {
        byte[] bytes = [(byte)'h', 0x93, (byte)'i', 0x94, 0xA9];

        var result = EncodingDetector.Detect(bytes);
        var act = () => result.Encoding.GetString(bytes);

        act.Should().NotThrow();
    }

    [Fact]
    public void Detect_EmptyInput_DefaultsToUtf8()
    {
        var result = EncodingDetector.Detect([]);

        result.Encoding.CodePage.Should().Be(Encoding.UTF8.CodePage);
        result.IsFallback.Should().BeFalse();
    }
}
