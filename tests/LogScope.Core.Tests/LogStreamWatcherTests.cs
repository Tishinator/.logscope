using FluentAssertions;
using LogScope.Core.Streaming;

namespace LogScope.Core.Tests;

public class LogStreamWatcherTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose() => File.Delete(_tempFile);

    [Fact]
    public async Task Watch_EmitsNewLines_WhenContentIsAppended()
    {
        File.WriteAllText(_tempFile, "first line\n");

        var received = new List<string>();
        using var watcher = new LogStreamWatcher(_tempFile);
        watcher.NewLinesAvailable += lines => received.AddRange(lines.Select(l => l.Text));

        await watcher.StartAsync();

        File.AppendAllText(_tempFile, "second line\n");
        File.AppendAllText(_tempFile, "third line\n");

        await Task.Delay(1500); // allow ~1 second polling window

        received.Should().Contain("second line");
        received.Should().Contain("third line");
    }

    [Fact]
    public async Task Watch_DoesNotReemit_AlreadyReadLines()
    {
        File.WriteAllText(_tempFile, "existing line\n");

        var received = new List<string>();
        using var watcher = new LogStreamWatcher(_tempFile);
        watcher.NewLinesAvailable += lines => received.AddRange(lines.Select(l => l.Text));

        await watcher.StartAsync();
        await Task.Delay(300);

        File.AppendAllText(_tempFile, "new line\n");
        await Task.Delay(1500);

        received.Should().NotContain("existing line");
        received.Should().Contain("new line");
    }

    [Fact]
    public async Task Watch_EmitsLines_WithCorrectSequentialLineNumbers()
    {
        File.WriteAllText(_tempFile, "line one\nline two\n");

        var received = new List<int>();
        using var watcher = new LogStreamWatcher(_tempFile);
        watcher.NewLinesAvailable += lines => received.AddRange(lines.Select(l => l.LineNumber));

        await watcher.StartAsync();
        File.AppendAllText(_tempFile, "line three\n");
        await Task.Delay(1500);

        received.Should().Contain(3);
    }

    [Fact]
    public async Task Stop_CeasesEmission_AfterCalled()
    {
        File.WriteAllText(_tempFile, "");

        var received = new List<string>();
        using var watcher = new LogStreamWatcher(_tempFile);
        watcher.NewLinesAvailable += lines => received.AddRange(lines.Select(l => l.Text));

        await watcher.StartAsync();
        watcher.Stop();

        File.AppendAllText(_tempFile, "should not arrive\n");
        await Task.Delay(1500);

        received.Should().BeEmpty();
    }
}
