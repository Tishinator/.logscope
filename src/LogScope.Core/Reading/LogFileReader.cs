using System.Text;

namespace LogScope.Core.Reading;

public sealed class LogFileReader
{
    public IEnumerable<RawLogLine> ReadLines(string path) => ReadLines(path, encoding: null);

    /// <summary>
    /// Reads lines lazily. When <paramref name="encoding"/> is null, the encoding is
    /// detected (BOM / UTF-8 / ANSI fallback per SR-04).
    /// </summary>
    public IEnumerable<RawLogLine> ReadLines(string path, Encoding? encoding)
    {
        if (!File.Exists(path))
            throw new LogFileNotFoundException(path);

        encoding ??= EncodingDetector.DetectFromFile(path).Encoding;
        return ReadLinesCore(path, encoding);
    }

    private static IEnumerable<RawLogLine> ReadLinesCore(string path, Encoding encoding)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);

        int lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            yield return new RawLogLine(lineNumber, line);
        }
    }
}
