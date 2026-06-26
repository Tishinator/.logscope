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
    public async Task StartAfterLines_ResumesAfterLoadedContent_AndEmitsTheGap()
    {
        // Viewer loaded 2 lines; file already grew to 4 before streaming was enabled.
        File.WriteAllText(_tempFile, "line one\nline two\nline three\nline four\n");

        var received = new List<(int Line, string Text)>();
        using var watcher = new LogStreamWatcher(_tempFile);
        watcher.NewLinesAvailable += lines => received.AddRange(lines.Select(l => (l.LineNumber, l.Text)));

        await watcher.StartAsync(startAfterLines: 2);
        await Task.Delay(1200);

        // Lines 3 and 4 (appended before streaming started) must still arrive, with correct numbers.
        received.Should().Contain((3, "line three"));
        received.Should().Contain((4, "line four"));
        received.Should().NotContain(r => r.Text == "line one");
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

    // -- New reliability tests (issue #33) --

    [Fact]
    public async Task PartialWrite_DoesNotEmitLine_UntilNewlineArrives()
    {
        // Write a partial line (no newline) — must not be emitted yet.
        File.WriteAllText(_tempFile, "partial");

        var received = new List<string>();
        using var watcher = new LogStreamWatcher(_tempFile);
        watcher.NewLinesAvailable += lines => received.AddRange(lines.Select(l => l.Text));

        await watcher.StartAsync(0);
        await Task.Delay(1200);

        received.Should().BeEmpty("partial write without newline must not be emitted");

        // Complete the line.
        File.AppendAllText(_tempFile, " line complete\n");
        await Task.Delay(1200);

        received.Should().ContainSingle().Which.Should().Be("partial line complete");
    }

    [Fact]
    public async Task CrlfSplitAcrossWrites_EmitsCorrectLine()
    {
        // CR written first, LF written second (CRLF across two polls).
        File.WriteAllText(_tempFile, "crlf line\r");

        var received = new List<string>();
        using var watcher = new LogStreamWatcher(_tempFile);
        watcher.NewLinesAvailable += lines => received.AddRange(lines.Select(l => l.Text));

        await watcher.StartAsync(0);
        await Task.Delay(1200);

        received.Should().BeEmpty("CR without LF is still partial");

        File.AppendAllText(_tempFile, "\nnext line\n");
        await Task.Delay(1200);

        received.Should().Contain("crlf line");
        received.Should().Contain("next line");
    }

    [Fact]
    public async Task FileTruncation_RaisesFileReset_AndReadsNewContent()
    {
        // Write a long initial content so truncation is clearly detectable by size.
        File.WriteAllText(_tempFile, string.Concat(Enumerable.Repeat("old content line\n", 20)));

        var received = new List<string>();
        var resetFired = false;
        using var watcher = new LogStreamWatcher(_tempFile);
        watcher.NewLinesAvailable += lines => received.AddRange(lines.Select(l => l.Text));
        watcher.FileReset += () => resetFired = true;

        await watcher.StartAsync();
        await Task.Delay(800);

        // Truncate and write shorter new content — file is now clearly smaller than before.
        File.WriteAllText(_tempFile, "after reset\n");
        await Task.Delay(1200);

        resetFired.Should().BeTrue("truncation must fire FileReset");
        received.Should().Contain("after reset");
    }
}
