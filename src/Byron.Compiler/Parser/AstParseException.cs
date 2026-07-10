using Byron.Compiler.Lexer;

namespace Byron.Compiler.Parser;

public class ByronParserException : Exception
{
    public SourceSpan Span { get; }
    
    public ByronParserException(string message, SourceSpan span) : base($"{message}")
    {
        Span = span;
    }

    public ByronParserException(Token token) : this($"Invalid token {token.Lexeme}", token.Span)
    {
    }
}
