namespace LogScope.Core.Parsing;

public sealed class InvalidParserProfileException : Exception
{
    public InvalidParserProfileException(string message, Exception inner)
        : base(message, inner) { }
}
