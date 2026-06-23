using System.Text;

namespace LogScope.Core.Reading;

/// <summary>
/// A byte-offset index of line starts, enabling random access to any line range of a
/// large file without loading the whole thing into memory (SR-05). The index stores one
/// 8-byte offset per line; file content is read on demand for the requested range only.
/// </summary>
public sealed class FileLineIndex
{
    private readonly string _path;
    private readonly Encoding _encoding;
    private readonly long[] _offsets; // byte offset of the start of each line (0-based)

    public long LineCount => _offsets.Length;
    public Encoding Encoding => _encoding;

    private FileLineIndex(string path, Encoding encoding, long[] offsets)
    {
        _path = path;
        _encoding = encoding;
        _offsets = offsets;
    }

    public static FileLineIndex Build(
        string path,
        Encoding? encoding = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new LogFileNotFoundException(path);

        encoding ??= EncodingDetector.DetectFromFile(path).Encoding;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        long length = stream.Length;

        // Skip a leading BOM so line 1 starts at a valid character boundary.
        long firstLineStart = SkipPreamble(stream, encoding);

        var offsets = new List<long>();
        if (length > firstLineStart)
            offsets.Add(firstLineStart);

        const int bufferSize = 1 << 16;
        var buffer = new byte[bufferSize];
        long position = firstLineStart;
        stream.Seek(firstLineStart, SeekOrigin.Begin);

        int reportCounter = 0;
        int read;
        while ((read = stream.Read(buffer, 0, bufferSize)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    long next = position + i + 1;
                    if (next < length)
                        offsets.Add(next);
                }
            }
            position += read;

            if (++reportCounter % 16 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(length == 0 ? 1.0 : (double)position / length);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(1.0);

        return new FileLineIndex(path, encoding, offsets.ToArray());
    }

    /// <summary>Reads <paramref name="count"/> lines starting at the 1-based physical line number.</summary>
    public IReadOnlyList<RawLogLine> ReadRange(long startLine, int count)
    {
        if (startLine < 1) startLine = 1;
        long startIndex = startLine - 1;
        if (startIndex >= _offsets.Length || count <= 0)
            return [];

        long startOffset = _offsets[startIndex];

        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(startOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, _encoding, detectEncodingFromByteOrderMarks: false);

        var rows = new List<RawLogLine>(count);
        long lineNumber = startLine;
        string? line;
        while (rows.Count < count && (line = reader.ReadLine()) != null)
        {
            rows.Add(new RawLogLine((int)Math.Min(lineNumber, int.MaxValue), line));
            lineNumber++;
        }
        return rows;
    }

    private static long SkipPreamble(FileStream stream, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0 || stream.Length < preamble.Length)
            return 0;

        var head = new byte[preamble.Length];
        stream.Seek(0, SeekOrigin.Begin);
        stream.Read(head, 0, head.Length);
        stream.Seek(0, SeekOrigin.Begin);

        for (int i = 0; i < preamble.Length; i++)
            if (head[i] != preamble[i])
                return 0;

        return preamble.Length;
    }
}
