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

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // Seek to end-of-file so we only emit newly appended content
        if (File.Exists(_path))
        {
            using var probe = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _bytesRead = probe.Length;
            // Count existing lines so new line numbers continue from there
            probe.Seek(0, SeekOrigin.Begin);
            using var counter = new StreamReader(probe, leaveOpen: true);
            while (counter.ReadLine() != null)
                _linesRead++;
        }

        Task.Run(() => PollLoop(token), token);
        return Task.CompletedTask;
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
