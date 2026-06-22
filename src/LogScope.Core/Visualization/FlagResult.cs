namespace LogScope.Core.Visualization;

public sealed class FlagResult
{
    public int FlaggedCount => FlaggedLineNumbers.Count;
    public IReadOnlyList<int> FlaggedLineNumbers { get; }

    public FlagResult(IReadOnlyList<int> flaggedLineNumbers)
    {
        FlaggedLineNumbers = flaggedLineNumbers;
    }
}
