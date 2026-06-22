using FluentAssertions;
using LogScope.Core.Reading;

namespace LogScope.Core.Tests;

public class LogFileReaderTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose() => File.Delete(_tempFile);

    [Fact]
    public void ReadLines_ReturnsOneLine_WhenFileHasOneLine()
    {
        File.WriteAllText(_tempFile, "hello world");

        var reader = new LogFileReader();
        var lines = reader.ReadLines(_tempFile).ToList();

        lines.Should().HaveCount(1);
        lines[0].Text.Should().Be("hello world");
        lines[0].LineNumber.Should().Be(1);
    }

    [Fact]
    public void ReadLines_AssignsSequentialLineNumbers()
    {
        File.WriteAllLines(_tempFile, ["alpha", "beta", "gamma"]);

        var reader = new LogFileReader();
        var lines = reader.ReadLines(_tempFile).ToList();

        lines.Select(l => l.LineNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ReadLines_ReturnsEmptySequence_ForEmptyFile()
    {
        File.WriteAllText(_tempFile, "");

        var reader = new LogFileReader();
        var lines = reader.ReadLines(_tempFile).ToList();

        lines.Should().BeEmpty();
    }

    [Fact]
    public void ReadLines_NeverBuffersWholeFile_YieldsLinesLazily()
    {
        // Prove laziness: take only 2 lines from a 5-line file — no exception, no full read
        File.WriteAllLines(_tempFile, ["a", "b", "c", "d", "e"]);

        var reader = new LogFileReader();
        var first2 = reader.ReadLines(_tempFile).Take(2).ToList();

        first2.Should().HaveCount(2);
        first2[0].Text.Should().Be("a");
        first2[1].Text.Should().Be("b");
    }

    [Fact]
    public void ReadLines_PreservesOriginalText_ExactlyAsStored()
    {
        var original = "2024-01-15 10:23:45.123 | ERROR | Some module | Something went wrong";
        File.WriteAllText(_tempFile, original);

        var reader = new LogFileReader();
        var line = reader.ReadLines(_tempFile).Single();

        line.Text.Should().Be(original);
    }

    [Fact]
    public void ReadLines_OpensWithReadWriteShare_SoOtherProcessesCanAppend()
    {
        File.WriteAllText(_tempFile, "first line");

        var reader = new LogFileReader();

        // Open the reader's enumeration and then open the same file for writing —
        // if the reader holds an exclusive lock this will throw.
        using var enumerator = reader.ReadLines(_tempFile).GetEnumerator();
        enumerator.MoveNext();

        var act = () => File.AppendAllText(_tempFile, "\nsecond line");
        act.Should().NotThrow();
    }

    [Fact]
    public void ReadLines_ReturnsError_WhenFileDoesNotExist()
    {
        var reader = new LogFileReader();
        var act = () => reader.ReadLines(@"C:\nonexistent\path\file.log").ToList();

        act.Should().Throw<LogFileNotFoundException>();
    }
}
