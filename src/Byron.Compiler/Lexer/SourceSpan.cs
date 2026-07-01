namespace Byron.Compiler.Lexer;

public record SourceSpan(int Line, int Column, int Start, int End)
{
    public static SourceSpan Empty => new(0, 0, 0, 0);

    public override string ToString() => $"({Line}:{Column})";
}
