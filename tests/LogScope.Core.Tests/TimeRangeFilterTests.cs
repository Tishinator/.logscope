using FluentAssertions;
using LogScope.Core.Filtering;
using LogScope.Core.Parsing;

namespace LogScope.Core.Tests;

public class TimeRangeFilterTests
{
    private static ParsedRow Row(int line, string ts) =>
        new(line, new Dictionary<string, string> { ["Timestamp"] = ts, ["Message"] = "m" });

    private static readonly ParsedRow[] Rows =
    [
        Row(1, "2024-01-15 10:00:00"),
        Row(2, "2024-01-15 10:05:00"),
        Row(3, "2024-01-15 10:10:00"),
        Row(4, "not a timestamp"),
    ];

    private static DateTime T(string hms) => DateTime.Parse("2024-01-15 " + hms);

    [Fact]
    public void Apply_FromAndTo_KeepsRowsInRange()
    {
        var result = TimeRangeFilter.Apply(Rows, "Timestamp", T("10:02:00"), T("10:08:00")).ToList();
        result.Select(r => r.LineNumber).Should().Equal(2);
    }

    [Fact]
    public void Apply_FromOnly_KeepsRowsAtOrAfter()
    {
        var result = TimeRangeFilter.Apply(Rows, "Timestamp", T("10:05:00"), null).ToList();
        result.Select(r => r.LineNumber).Should().Equal(2, 3);
    }

    [Fact]
    public void Apply_ToOnly_KeepsRowsAtOrBefore()
    {
        var result = TimeRangeFilter.Apply(Rows, "Timestamp", null, T("10:05:00")).ToList();
        result.Select(r => r.LineNumber).Should().Equal(1, 2);
    }

    [Fact]
    public void Apply_ExcludesUnparseableTimestamps_WhenBounded()
    {
        var result = TimeRangeFilter.Apply(Rows, "Timestamp", T("09:00:00"), T("11:00:00")).ToList();
        result.Select(r => r.LineNumber).Should().NotContain(4);
    }

    [Fact]
    public void Apply_NoBounds_ReturnsAll()
    {
        TimeRangeFilter.Apply(Rows, "Timestamp", null, null).Should().HaveCount(4);
    }

    [Fact]
    public void Apply_NoTimestampField_ReturnsAll()
    {
        TimeRangeFilter.Apply(Rows, null, DateTime.MinValue, DateTime.MaxValue).Should().HaveCount(4);
    }
}
