using FluentAssertions;
using LogScope.Core.Sync;

namespace LogScope.Core.Tests;

public class TimestampParserTests
{
    [Theory]
    [InlineData("2024-01-15 10:32:39")]
    [InlineData("2024-01-15 10:32:39.123")]
    [InlineData("2024-01-15T10:32:39")]
    [InlineData("10:32:39")]
    [InlineData("01/15/2024 10:32:39")]
    public void TryParse_ParsesCommonFormats(string input)
    {
        TimestampParser.TryParse(input).Should().NotBeNull();
    }

    [Fact]
    public void TryParse_ParsesHealthAppFormat()
    {
        // yyyyMMdd-HH:mm:ss:fff (the LogHub HealthApp dataset)
        var ts = TimestampParser.TryParse("20171223-22:15:29:606");

        ts.Should().NotBeNull();
        ts!.Value.Year.Should().Be(2017);
        ts.Value.Hour.Should().Be(22);
    }

    [Fact]
    public void TryParse_ReturnsNull_ForNonTimestamp()
    {
        TimestampParser.TryParse("not a time").Should().BeNull();
        TimestampParser.TryParse("").Should().BeNull();
    }

    [Fact]
    public void TryParse_OrdersChronologically()
    {
        var a = TimestampParser.TryParse("2024-01-15 10:00:00")!.Value;
        var b = TimestampParser.TryParse("2024-01-15 10:00:01")!.Value;
        b.Should().BeAfter(a);
    }
}

public class SyncAlignerTests
{
    [Fact]
    public void AlignByLine_ClampsToTargetRange()
    {
        SyncAligner.AlignByLine(5, targetLineCount: 10).Should().Be(5);
        SyncAligner.AlignByLine(99, targetLineCount: 10).Should().Be(10);
        SyncAligner.AlignByLine(0, targetLineCount: 10).Should().Be(1);
    }

    [Fact]
    public void NearestByTimestamp_FindsClosestRow()
    {
        var rows = new (int, DateTime)[]
        {
            (1, T("10:00:00")),
            (2, T("10:00:10")),
            (3, T("10:00:20")),
        };

        SyncAligner.NearestByTimestamp(rows, T("10:00:11")).Should().Be(2);
        SyncAligner.NearestByTimestamp(rows, T("10:00:18")).Should().Be(3);
        SyncAligner.NearestByTimestamp(rows, T("09:59:00")).Should().Be(1);
    }

    [Fact]
    public void NearestByTimestamp_ReturnsNull_WhenNoRows()
    {
        SyncAligner.NearestByTimestamp([], T("10:00:00")).Should().BeNull();
    }

    private static DateTime T(string hms) => DateTime.Parse("2024-01-15 " + hms);
}
