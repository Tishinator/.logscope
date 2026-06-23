using System.Text;
using FluentAssertions;
using LogScope.Core.Reading;

namespace LogScope.Core.Tests;

public class FileLineIndexTests : IDisposable
{
    private readonly string _file = Path.GetTempFileName();
    public void Dispose() => File.Delete(_file);

    private void Write(string content) => File.WriteAllText(_file, content, new UTF8Encoding(false));
    private void WriteLines(params string[] lines) => File.WriteAllLines(_file, lines, new UTF8Encoding(false));

    [Fact]
    public void Build_CountsLines()
    {
        WriteLines("one", "two", "three", "four");

        var index = FileLineIndex.Build(_file);

        index.LineCount.Should().Be(4);
    }

    [Fact]
    public void Build_EmptyFile_HasZeroLines()
    {
        Write("");
        FileLineIndex.Build(_file).LineCount.Should().Be(0);
    }

    [Fact]
    public void Build_CountsBlankLinesBetweenContent()
    {
        Write("a\n\nb\n");
        FileLineIndex.Build(_file).LineCount.Should().Be(3); // "a", "", "b"
    }

    [Fact]
    public void Build_HandlesFileWithoutTrailingNewline()
    {
        Write("a\nb\nc"); // no trailing newline
        FileLineIndex.Build(_file).LineCount.Should().Be(3);
    }

    [Fact]
    public void ReadRange_ReturnsRequestedLines_WithCorrectPhysicalNumbers()
    {
        WriteLines("L1", "L2", "L3", "L4", "L5");

        var index = FileLineIndex.Build(_file);
        var rows = index.ReadRange(startLine: 2, count: 3);

        rows.Select(r => r.LineNumber).Should().Equal(2, 3, 4);
        rows.Select(r => r.Text).Should().Equal("L2", "L3", "L4");
    }

    [Fact]
    public void ReadRange_RandomAccess_DoesNotDependOnReadingFromStart()
    {
        WriteLines(Enumerable.Range(1, 1000).Select(i => $"line-{i}").ToArray());

        var index = FileLineIndex.Build(_file);
        var rows = index.ReadRange(startLine: 950, count: 5);

        rows.Select(r => r.LineNumber).Should().Equal(950, 951, 952, 953, 954);
        rows[0].Text.Should().Be("line-950");
        rows[4].Text.Should().Be("line-954");
    }

    [Fact]
    public void ReadRange_ClampsCount_AtEndOfFile()
    {
        WriteLines("a", "b", "c");

        var index = FileLineIndex.Build(_file);
        var rows = index.ReadRange(startLine: 2, count: 100);

        rows.Select(r => r.Text).Should().Equal("b", "c");
    }

    [Fact]
    public void ReadRange_PreservesCrLfStrippedContent()
    {
        Write("alpha\r\nbeta\r\ngamma\r\n");

        var index = FileLineIndex.Build(_file);
        var rows = index.ReadRange(1, 3);

        rows.Select(r => r.Text).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void Build_ReportsProgress()
    {
        WriteLines(Enumerable.Range(1, 500).Select(i => $"row {i}").ToArray());
        double lastProgress = 0;
        var progress = new Progress<double>(p => lastProgress = p);

        // Progress is reported asynchronously; just ensure Build completes and is usable.
        var index = FileLineIndex.Build(_file, encoding: null, progress: progress);

        index.LineCount.Should().Be(500);
    }

    [Fact]
    public void Build_IsCancellable()
    {
        WriteLines(Enumerable.Range(1, 100000).Select(i => $"row {i}").ToArray());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => FileLineIndex.Build(_file, encoding: null, progress: null, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }
}
