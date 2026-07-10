namespace Byron.Compiler.Lexer;

/// <summary>
/// Represents a token from source code
/// </summary>
/// <param name="Kind">The TokenKind that the Lexeme represents</param>
/// <param name="Lexeme">The raw source text</param>
/// <param name="Value">The parsed value</param>
/// <param name="Span">Line and column information for better error messages</param>
public record Token(
    TokenKind   Kind,
    string      Lexeme,
    object?     Value,
    SourceSpan  Span)
{
    public static Token Create(TokenKind kind, string lexeme, SourceSpan span) => new(kind, lexeme, null, span);

    public static Token CreateWithValue(TokenKind kind, string lexeme, object value, SourceSpan span)
        => new(kind, lexeme, value, span);

    public static Token Error(string lexeme, string message, SourceSpan span)
        => new(TokenKind.LexError, lexeme, message, span);

    public override string ToString()
        => Value is not null
            ? $"[{Kind} \"{Lexeme}\" = {Value} @ {Span}]"
            : $"[{Kind} \"{Lexeme}\" @ {Span}]";
}
