using System.Text;

namespace LogScope.Core.Reading;

public sealed class EncodingDetectionResult
{
    public Encoding Encoding { get; }
    public string EncodingName { get; }
    public bool HadBom { get; }
    public bool IsFallback { get; }
    public string? Warning { get; }

    public EncodingDetectionResult(Encoding encoding, string encodingName, bool hadBom, bool isFallback, string? warning)
    {
        Encoding = encoding;
        EncodingName = encodingName;
        HadBom = hadBom;
        IsFallback = isFallback;
        Warning = warning;
    }
}

/// <summary>
/// Detects text encoding (SR-04): UTF-8/UTF-16 via BOM, validated UTF-8 without BOM,
/// otherwise a non-throwing Windows-1252 (ANSI) fallback with a warning.
/// </summary>
public static class EncodingDetector
{
    private static readonly Encoding Ansi;

    static EncodingDetector()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // Windows-1252 with replacement fallback so malformed bytes never throw.
        Ansi = Encoding.GetEncoding(1252,
            EncoderFallback.ReplacementFallback,
            DecoderFallback.ReplacementFallback);
    }

    public static EncodingDetectionResult Detect(byte[] sample)
    {
        if (sample.Length >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
            return new(Encoding.UTF8, "UTF-8 (BOM)", hadBom: true, isFallback: false, warning: null);

        if (sample.Length >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
            return new(Encoding.Unicode, "UTF-16 LE", hadBom: true, isFallback: false, warning: null);

        if (sample.Length >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
            return new(Encoding.BigEndianUnicode, "UTF-16 BE", hadBom: true, isFallback: false, warning: null);

        if (sample.Length == 0 || IsValidUtf8(sample))
            return new(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), "UTF-8", hadBom: false, isFallback: false, warning: null);

        return new(Ansi, "Windows-1252 (ANSI)", hadBom: false, isFallback: true,
            warning: "File is not valid UTF-8; decoded as Windows-1252 (ANSI). Some characters may be approximate.");
    }

    public static EncodingDetectionResult DetectFromFile(string path, int sampleBytes = 64 * 1024)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[Math.Min(sampleBytes, (int)Math.Min(stream.Length, int.MaxValue))];
        int read = stream.Read(buffer, 0, buffer.Length);
        if (read != buffer.Length)
            Array.Resize(ref buffer, read);
        return Detect(buffer);
    }

    private static bool IsValidUtf8(byte[] bytes)
    {
        // A strict UTF-8 decode throws on invalid sequences.
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            strict.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
