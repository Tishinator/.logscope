using LogScope.Core.Reading;

namespace LogScope.Core.Streaming;

public sealed class LogStreamWatcher : IDisposable
{
    private readonly string _path;
    private readonly TimeSpan _pollInterval;
    private CancellationTokenSource? _cts;
    private long _bytesRead;
    private int _linesRead;

    public event Action<IReadOnlyList<RawLogLine>>? NewLinesAvailable;

    public LogStreamWatcher(string path, TimeSpan? pollInterval = null)
    {
        _path = path;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// Begins watching for appended content. When <paramref name="startAfterLines"/> is given,
    /// streaming resumes after that many physical lines (i.e. right after the content already
    /// loaded by the viewer), so nothing appended before streaming was enabled is skipped.
    /// When omitted (-1), it seeks to the current end of file.
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

    /// <summary>Byte offset just past the Nth newline (start of line N+1), or EOF if fewer lines.</summary>
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
                // file temporarily locked; retry next cycle
            }
        }
    }

    private void ReadNewLines()
    {
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (stream.Length <= _bytesRead)
            return;

        stream.Seek(_bytesRead, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, leaveOpen: true);

        var newLines = new List<RawLogLine>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            _linesRead++;
            newLines.Add(new RawLogLine(_linesRead, line));
        }

        _bytesRead = stream.Position;

        if (newLines.Count > 0)
            NewLinesAvailable?.Invoke(newLines);
    }

    public void Dispose() => _cts?.Dispose();
}
