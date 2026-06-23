using FluentAssertions;
using LogScope.Core.Documents;

namespace LogScope.Core.Tests;

public class LogDocumentLoadCapTests : IDisposable
{
    private readonly string _file = Path.GetTempFileName();
    public void Dispose() => File.Delete(_file);

    [Fact]
    public void Load_CapsRowsAtMaxLines_AndFlagsTruncated()
    {
        File.WriteAllLines(_file, Enumerable.Range(1, 100).Select(i => $"line {i}"));

        var doc = LogDocument.Load(_file, LogProfile.Raw(), encoding: null, maxLines: 40);

        doc.Rows.Should().HaveCount(40);
        doc.Truncated.Should().BeTrue();
        doc.Rows[^1].Fields["RawText"].Should().Be("line 40");
    }

    [Fact]
    public void Load_NotTruncated_WhenUnderCap()
    {
        File.WriteAllLines(_file, Enumerable.Range(1, 10).Select(i => $"line {i}"));

        var doc = LogDocument.Load(_file, LogProfile.Raw(), encoding: null, maxLines: 40);

        doc.Truncated.Should().BeFalse();
        doc.Rows.Should().HaveCount(10);
    }

    [Fact]
    public void Load_ExposesEncodingName()
    {
        File.WriteAllText(_file, "plain ascii line");

        var doc = LogDocument.Load(_file, LogProfile.Raw());

        doc.EncodingName.Should().NotBeNullOrEmpty();
    }
}
