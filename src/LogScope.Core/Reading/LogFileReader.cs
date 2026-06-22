namespace LogScope.Core.Reading;

public sealed class LogFileReader
{
    public IEnumerable<RawLogLine> ReadLines(string path)
    {
        if (!File.Exists(path))
            throw new LogFileNotFoundException(path);

        return ReadLinesCore(path);
    }

    private static IEnumerable<RawLogLine> ReadLinesCore(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        int lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            yield return new RawLogLine(lineNumber, line);
        }
    }
}
