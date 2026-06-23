namespace LogScope.Core.Parsing;

public enum DetectedFormatKind { Delimited, Raw }

public sealed class FormatSuggestion
{
    public DetectedFormatKind Kind { get; }
    public string? Delimiter { get; }
    public int FieldCount { get; }
    public IReadOnlyList<string> SuggestedFieldNames { get; }

    public FormatSuggestion(DetectedFormatKind kind, string? delimiter, int fieldCount, IReadOnlyList<string> suggestedFieldNames)
    {
        Kind = kind;
        Delimiter = delimiter;
        FieldCount = fieldCount;
        SuggestedFieldNames = suggestedFieldNames;
    }
}

public sealed class FormatDetector
{
    // Ordered by preference: multi-char before single-char so "||" wins over "|"
    private static readonly string[] CandidateDelimiters = ["||", "\t", "|", ",", ";"];

    private static readonly string[] SemanticNames =
        ["Timestamp", "Level", "Module", "Message", "Thread", "DeviceId", "TestCase", "RunId", "Result"];

    public FormatSuggestion Detect(IReadOnlyList<string> sampleLines)
    {
        var nonEmpty = sampleLines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (nonEmpty.Count == 0)
            return Raw();

        foreach (var delimiter in CandidateDelimiters)
        {
            // Delimiter must appear in every sampled line and yield a consistent column count
            if (!nonEmpty.All(l => l.Contains(delimiter)))
                continue;

            var counts = nonEmpty
                .Select(l => l.Split(delimiter, StringSplitOptions.None).Length)
                .ToList();

            int fieldCount = counts[0];
            bool consistent = counts.All(c => c == fieldCount);

            if (consistent && fieldCount >= 2)
                return new FormatSuggestion(DetectedFormatKind.Delimited, delimiter, fieldCount, NameFields(fieldCount));
        }

        return Raw();
    }

    private static FormatSuggestion Raw() =>
        new(DetectedFormatKind.Raw, delimiter: null, fieldCount: 0, suggestedFieldNames: []);

    private static IReadOnlyList<string> NameFields(int count)
    {
        var names = new List<string>(count);
        for (int i = 0; i < count; i++)
            names.Add(i < SemanticNames.Length ? SemanticNames[i] : $"Field{i + 1}");
        return names;
    }
}
