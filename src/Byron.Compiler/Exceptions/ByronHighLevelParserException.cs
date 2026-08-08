using Byron.Compiler.Lexer;

namespace Byron.Compiler.Exceptions;

public class ByronHighLevelParserException(string message, SourceSpan span) : ByronException(message)
{
    public SourceSpan Span { get; } = span;

    public ByronHighLevelParserException(Token token) : this($"Invalid token {token.Lexeme}", token.Span)
    {
    }
}
public class ByronLowLevelParserException(string message) : ByronException(message)
{
}