using FluentAssertions;
using LogScope.Core.Documents;

namespace LogScope.Core.Tests;

/// <summary>
/// Regression tests for issue #34: applying a format profile to an open tab must
/// produce rows with correctly-keyed Fields — if Fields doesn't contain the expected
/// column names, the DataGrid shows only the Line # column (empty data cells).
/// </summary>
public class LogDocumentProfileSwitchTests : IDisposable
{
    private readonly string _file = Path.GetTempFileName();

    public void Dispose() => File.Delete(_file);

    [Fact]
    public void DelimitedProfile_ProducesRowsWithAllFieldKeys()
    {
        File.WriteAllText(_file, "2024-01-15 10:00:00|INFO|Starting up\n2024-01-15 10:00:01|WARN|Low memory\n");
        var profile = LogProfile.Delimited("|", ["Timestamp", "Level", "Message"]);

        var doc = LogDocument.Load(_file, profile);

        doc.Rows.Should().HaveCount(2);
        doc.Rows[0].Fields.Keys.Should().Contain(["Timestamp", "Level", "Message"]);
        doc.Rows[0].Fields["Level"].Should().Be("INFO");
        doc.Rows[1].Fields["Level"].Should().Be("WARN");
    }

    [Fact]
    public void SwitchFromRawToDelimited_ProducesDelimitedFields()
    {
        File.WriteAllText(_file, "2024-01-15|INFO|msg\n");

        var rawDoc = LogDocument.Load(_file, LogProfile.Raw());
        rawDoc.Columns.Should().Contain("RawText");

        var delimProfile = LogProfile.Delimited("|", ["Timestamp", "Level", "Message"]);
        var delimDoc = LogDocument.Load(_file, delimProfile);

        delimDoc.Columns.Should().BeEquivalentTo(["Timestamp", "Level", "Message"]);
        delimDoc.Rows[0].Fields.Should().ContainKey("Timestamp");
        delimDoc.Rows[0].Fields.Should().ContainKey("Level");
        delimDoc.Rows[0].Fields.Should().ContainKey("Message");
        delimDoc.Rows[0].Fields.Should().NotContainKey("RawText");
    }

    [Fact]
    public void FallbackRows_ContainRawTextKey_NotProfileFieldNames()
    {
        // Lines without the delimiter fall back to {"RawText": ...}.
        // The DataGrid would show empty cells for profile field columns.
        // This test documents the known behavior — callers must handle fallback rows.
        File.WriteAllText(_file, "this line has no delimiter\n");
        var profile = LogProfile.Delimited("|", ["Timestamp", "Level", "Message"]);

        var doc = LogDocument.Load(_file, profile);

        doc.Rows[0].Fields.Should().ContainKey("RawText");
        doc.Rows[0].Fields.Should().NotContainKey("Timestamp");
    }
}
