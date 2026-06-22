namespace LogScope.Core.Reading;

public sealed class LogFileNotFoundException : Exception
{
    public LogFileNotFoundException(string path)
        : base($"Log file not found: {path}") { }
}
