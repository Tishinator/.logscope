using System.Text;
using LogScope.Core.Reading;

namespace LogScope.Core.Streaming;

/// <summary>
/// Polls a log file for appended content and raises <see cref="NewLinesAvailable"/> with complete
/// lines only — partial writes (no trailing newline yet) are held in a byte buffer until a newline
/// arrives.  File truncation and rotation are detected and reset cleanly (SR-05 / issue #33).
/// </summary>
public sealed class LogStreamWatcher : IDisposable
{
    private readonly string _path;
    private readonly TimeSpan _pollInterval;
    private CancellationTokenSource? _cts;
    private long _bytesRead;
    private int _linesRead;

    // Bytes received after the last '\n' — held until a newline arrives (partial-write guard).
    private byte[] _partial = [];
    private int _partialLen;

    // Per-watcher decoder so multibyte sequences split across reads are handled correctly.
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

    public event Action<IReadOnlyList<RawLogLine>>? NewLinesAvailable;
    /// <summary>Raised when the file is truncated or replaced so the caller can reset its view.</summary>
    public event Action? FileReset;

    public LogStreamWatcher(string path, TimeSpan? pollInterval = null)
    {
        _path = path;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// Begins watching.  When <paramref name="startAfterLines"/> is given, streaming resumes
    /// right after that many physical lines (no skipping). When −1, seeks to current EOF.
    /// </summary>
    public Task StartAsync(int startAfterLines = -1)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (File.Exists(_path))
        {
            using var probe = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (startAfterLines < 0)
            {
                _bytesRead = probe.Length;
                probe.Seek(0, SeekOrigin.Begin);
                using var counter = new StreamReader(probe, leaveOpen: true);
                while (counter.ReadLine() != null)
                    _linesRead++;
            }
            else
            {
                _bytesRead = OffsetAfterLines(probe, startAfterLines);
                _linesRead = startAfterLines;
            }
        }

        Task.Run(() => PollLoop(token), token);
        return Task.CompletedTask;
    }

    private static long OffsetAfterLines(FileStream stream, int lines)
    {
        if (lines <= 0) return 0;

        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[1 << 16];
        long pos = 0;
        int newlines = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                pos++;
                if (buffer[i] == (byte)'\n' && ++newlines >= lines)
                    return pos;
            }
        }
        return stream.Length;
    }

    public void Stop() => _cts?.Cancel();

    private async Task PollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(_pollInterval, token).ContinueWith(_ => { });

            if (token.IsCancellationRequested)
                break;

            try
            {
                ReadNewLines();
            }
            catch (IOException)
            {
                // File temporarily locked or absent — retry next cycle.
            }
            catch (Exception)
            {
                // Don't let any unexpected exception kill the watcher.
            }
        }
    }

    private void ReadNewLines()
    {
        if (!File.Exists(_path)) return;

        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        // Detect truncation / rotation.
        if (stream.Length < _bytesRead)
        {
            _bytesRead = 0;
            _linesRead = 0;
            _partialLen = 0;
            _decoder.Reset();
            FileReset?.Invoke();
        }

        if (stream.Length == _bytesRead) return;

        stream.Seek(_bytesRead, SeekOrigin.Begin);

        // Read new raw bytes.
        var rawBytes = new byte[stream.Length - _bytesRead];
        int totalRead = stream.Read(rawBytes, 0, rawBytes.Length);
        if (totalRead == 0) return;

        _bytesRead = stream.Position;

        // Prepend any buffered partial line from the previous poll.
        byte[] data;
        if (_partialLen > 0)
        {
            data = new byte[_partialLen + totalRead];
            Array.Copy(_partial, data, _partialLen);
            Array.Copy(rawBytes, 0, data, _partialLen, totalRead);
            _partialLen = 0;
        }
        else
        {
            data = rawBytes.Length == totalRead ? rawBytes : rawBytes[..totalRead];
        }

        // Split on '\n', keeping only complete lines and buffering any trailing partial.
        var newLines = new List<RawLogLine>();
        int segStart = 0;

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == (byte)'\n')
            {
                int segEnd = i;
                // Strip optional '\r' before '\n' for CRLF files.
                if (segEnd > segStart && data[segEnd - 1] == (byte)'\r')
                    segEnd--;

                string text = Encoding.UTF8.GetString(data, segStart, segEnd - segStart);
                _linesRead++;
                newLines.Add(new RawLogLine(_linesRead, text));
                segStart = i + 1;
            }
        }

        // Buffer remaining bytes (no trailing newline yet).
        int remaining = data.Length - segStart;
        if (remaining > 0)
        {
            if (_partial.Length < remaining)
                _partial = new byte[remaining * 2];
            Array.Copy(data, segStart, _partial, 0, remaining);
            _partialLen = remaining;
        }

        if (newLines.Count > 0)
            NewLinesAvailable?.Invoke(newLines);
    }

    public void Dispose() => _cts?.Dispose();
}
