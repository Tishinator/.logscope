namespace LogScope.Core.Sync;

/// <summary>
/// Aligns a position in one log view to the equivalent position in another (SR-09),
/// either by physical line number or by nearest parsed timestamp. Logs are never merged.
/// </summary>
public static class SyncAligner
{
    public static int AlignByLine(int referenceLine, int targetLineCount)
    {
        if (targetLineCount < 1) return 1;
        return Math.Clamp(referenceLine, 1, targetLineCount);
    }

    /// <summary>Returns the line number of the row whose timestamp is closest to <paramref name="reference"/>.</summary>
    public static int? NearestByTimestamp(IReadOnlyList<(int Line, DateTime Timestamp)> targetRows, DateTime reference)
    {
        if (targetRows.Count == 0)
            return null;

        int bestLine = targetRows[0].Line;
        var bestDelta = (reference - targetRows[0].Timestamp).Duration();

        foreach (var (line, ts) in targetRows)
        {
            var delta = (reference - ts).Duration();
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestLine = line;
            }
        }

        return bestLine;
    }
}
