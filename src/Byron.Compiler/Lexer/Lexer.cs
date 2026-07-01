namespace Byron.Compiler.Lexer;

public class Lexer
{
    public List<Token> Tokenise()
    {
        List<Token> tokens = [Token.Make(TokenKind.Eof, "", SourceSpan.Empty)];

        return tokens;
    }
}