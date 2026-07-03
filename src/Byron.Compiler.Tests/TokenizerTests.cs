using Byron.Compiler.Lexer;
using Xunit;

namespace Byron.Compiler.Tests;

public class TokenizerTests
{
    private static List<Token> Lex(string source)
        => new Tokenizer(source).Tokenise();

    private static Token SingleToken(string source)
    {
        var tokens = Lex(source);
        Assert.Equal(2, tokens.Count); // tokens[1] is EoF which the lexer always appends
        return tokens[0]; 
    }

    [Theory]
    [InlineData("fn",        TokenKind.Fn)]
    [InlineData("pub",       TokenKind.Pub)]
    [InlineData("struct",    TokenKind.Struct)]
    [InlineData("let",       TokenKind.Let)]
    [InlineData("var",       TokenKind.Var)]
    [InlineData("give",      TokenKind.Give)]
    [InlineData("take",      TokenKind.Take)]
    [InlineData("return",    TokenKind.Return)]
    [InlineData("if",        TokenKind.If)]
    [InlineData("else",      TokenKind.Else)]
    [InlineData("while",     TokenKind.While)]
    [InlineData("defer",     TokenKind.Defer)]
    [InlineData("errordefer",TokenKind.ErrorDefer)]
    [InlineData("errdefer",  TokenKind.ErrorDefer)]
    [InlineData("onerror",   TokenKind.OnError)]
    [InlineData("import",    TokenKind.Import)]
    [InlineData("unsafe",    TokenKind.Unsafe)]
    [InlineData("untracked", TokenKind.Untracked)]
    [InlineData("comptime",  TokenKind.Comptime)]
    [InlineData("dynamic",   TokenKind.Dynamic)]
    [InlineData("void",      TokenKind.Void)]
    [InlineData("self",      TokenKind.Self)]
    [InlineData("Self",      TokenKind.CapitalSelf)]
    [InlineData("and",       TokenKind.And)]
    [InlineData("or",        TokenKind.Or)]
    [InlineData("union",     TokenKind.Union)]
    [InlineData("match",     TokenKind.Match)]
    public void Keywords_AreRecognised(string source, TokenKind expected)
    {
        var token = SingleToken(source);
        Assert.Equal(expected, token.Kind);
        Assert.Equal(source, token.Lexeme);
    }

    [Theory]
    [InlineData("true",  TokenKind.True)]
    [InlineData("false", TokenKind.False)]
    [InlineData("Ok",    TokenKind.Ok)]
    [InlineData("Some",  TokenKind.Some)]
    [InlineData("None",  TokenKind.None)]
    [InlineData("Error", TokenKind.Error)]
    public void ReservedIdentifiers_AreRecognised(string source, TokenKind expected)
    {
        var token = SingleToken(source);
        Assert.Equal(expected, token.Kind);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("myVariable")]
    [InlineData("_private")]
    [InlineData("camelCase123")]
    [InlineData("x")]
    public void Identifiers_AreRecognised(string source)
    {
        var token = SingleToken(source);
        Assert.Equal(TokenKind.Identifier, token.Kind);
        Assert.Equal(source, token.Lexeme);
    }

    [Theory]
    [InlineData("0",      0L)]
    [InlineData("00",      0L)]
    [InlineData("01",      1L)]
    [InlineData("42",     42L)]
    [InlineData("1_000",  1000L)]
    [InlineData("0xFF",   255L)]
    [InlineData("0b1010", 10L)]
    [InlineData("0o17",   15L)]
    public void IntLiterals_ParseCorrectly(string source, long expected)
    {
        var token = SingleToken(source);
        Assert.Equal(TokenKind.IntLiteral, token.Kind);
        Assert.Equal(expected, (long)token.Value!);
    }

    [Theory]
    [InlineData("3.14",  3.14)]
    [InlineData("1.0",   1.0)]
    [InlineData("2.5e3", 2500.0)]
    public void FloatLiterals_ParseCorrectly(string source, double expected)
    {
        var token = SingleToken(source);
        Assert.Equal(TokenKind.FloatLiteral, token.Kind);
        Assert.Equal(expected, (double)token.Value!, 5);
    }

    [Fact]
    public void StringLiteral_BasicString()
    {
        var token = SingleToken("\"hello\"");
        Assert.Equal(TokenKind.StringLiteral, token.Kind);
        Assert.Equal("hello", (string)token.Value!);
    }

    [Fact]
    public void StringLiteral_EscapeSequences()
    {
        var token = SingleToken("\"hello\\nworld\"");
        Assert.Equal(TokenKind.StringLiteral, token.Kind);
        Assert.Equal("hello\nworld", (string)token.Value!);
    }

    [Fact]
    public void StringLiteral_Unterminated_ProducesError()
    {
        var token = SingleToken("\"unterminated");
        Assert.Equal(TokenKind.LexError, token.Kind);
    }

    [Fact]
    public void RuneLiteral_BasicChar()
    {
        var token = SingleToken("'a'");
        Assert.Equal(TokenKind.RuneLiteral, token.Kind);
        Assert.Equal('a', (char)token.Value!);
    }

    [Fact]
    public void RuneLiteral_EscapedNewline()
    {
        var token = SingleToken("'\\n'");
        Assert.Equal(TokenKind.RuneLiteral, token.Kind);
        Assert.Equal('\n', (char)token.Value!);
    }

    [Theory]
    [InlineData("==", TokenKind.EqualsEquals)]
    [InlineData("!=", TokenKind.BangEquals)]
    [InlineData("<=", TokenKind.LessEquals)]
    [InlineData(">=", TokenKind.GreaterEquals)]
    [InlineData("&&", TokenKind.AmpAmp)]
    [InlineData("||", TokenKind.PipePipe)]
    [InlineData("+=", TokenKind.PlusEquals)]
    [InlineData("-=", TokenKind.MinusEquals)]
    [InlineData("*=", TokenKind.StarEquals)]
    [InlineData("/=", TokenKind.SlashEquals)]
    [InlineData("->", TokenKind.Arrow)]
    [InlineData("=>", TokenKind.FatArrow)]
    [InlineData("..", TokenKind.DotDot)]
    [InlineData("..=",TokenKind.DotDotEquals)]
    [InlineData("::", TokenKind.ColonColon)]
    [InlineData(">>", TokenKind.RAngleRAngle)]
    [InlineData("<<", TokenKind.LAngleLAngle)]
    public void Operators_MultiChar_AreRecognised(string source, TokenKind expected)
    {
        var token = SingleToken(source);
        Assert.Equal(expected, token.Kind);
        Assert.Equal(source, token.Lexeme);
    }

    [Fact]
    public void LineComment_IsEmitted()
    {
        var tokens = Lex("// this is a comment\nfn");
        Assert.Equal(TokenKind.LineComment, tokens[0].Kind);
        Assert.Equal(TokenKind.Fn, tokens[1].Kind);
    }

    [Fact]
    public void DocComment_IsEmitted()
    {
        var tokens = Lex("/// doc comment\nfn");
        Assert.Equal(TokenKind.DocComment, tokens[0].Kind);
    }

    [Fact]
    public void BlockComment_IsEmitted()
    {
        var tokens = Lex("/* block */fn");
        Assert.Equal(TokenKind.BlockComment, tokens[0].Kind);
        Assert.Equal(TokenKind.Fn, tokens[1].Kind);
    }

    [Fact]
    public void BlockComment_Nested_IsHandled()
    {
        var tokens = Lex("/* outer /* inner */ still outer */fn");
        Assert.Equal(TokenKind.BlockComment, tokens[0].Kind);
        Assert.Equal(TokenKind.Fn, tokens[1].Kind);
    }

    [Fact]
    public void Span_Line_And_Col_AreTracked()
    {
        var tokens = Lex("fn\n  foo");
        Assert.Equal(1, tokens[0].Span.Line);
        Assert.Equal(2, tokens[1].Span.Line);
        Assert.Equal(3, tokens[1].Span.Column);
    }

    [Fact]
    public void UnrecognisedChar_ProducesErrorToken()
    {
        var tokens = Lex("fn § foo");
        Assert.Contains(tokens, t => t.Kind == TokenKind.LexError);
        Assert.Contains(tokens, t => t.Kind == TokenKind.Fn);
        Assert.Contains(tokens, t => t is { Kind: TokenKind.Identifier, Lexeme: "foo" });
    }

    [Fact]
    public void EmptySource_ProducesOnlyEof()
    {
        var tokens = Lex("");
        Assert.Single(tokens);
        Assert.Equal(TokenKind.Eof, tokens[0].Kind);
    }

    [Fact]
    public void Snippet_FunctionDeclaration_LexesCorrectly()
    {
        var source = """
            pub fn add(a: i32, b: i32): i32 {
                return a + b;
            }
            """;

        var tokens = Lex(source);

        AssertTokenIsOfKind(tokens[0], TokenKind.Pub);
        AssertTokenIsOfKind(tokens[1], TokenKind.Fn);
        AssertTokenIsOfKind(tokens[2], TokenKind.Identifier);
        AssertLexemeEquals(tokens[2], "add");
        AssertTokenIsOfKind(tokens[3], TokenKind.LParen);
        AssertTokenIsOfKind(tokens[4], TokenKind.Identifier);
        AssertLexemeEquals(tokens[4], "a");
        AssertTokenIsOfKind(tokens[5], TokenKind.Colon);
        AssertTokenIsOfKind(tokens[6], TokenKind.Identifier);
        AssertLexemeEquals(tokens[6], "i32");
        AssertTokenIsOfKind(tokens[7], TokenKind.Comma);
        AssertTokenIsOfKind(tokens[8], TokenKind.Identifier);
        AssertLexemeEquals(tokens[8], "b");
        AssertTokenIsOfKind(tokens[9], TokenKind.Colon);
        AssertTokenIsOfKind(tokens[10], TokenKind.Identifier);
        AssertLexemeEquals(tokens[10], "i32");
        AssertTokenIsOfKind(tokens[11], TokenKind.RParen);
        AssertTokenIsOfKind(tokens[12], TokenKind.Colon);
        AssertTokenIsOfKind(tokens[13], TokenKind.Identifier);
        AssertLexemeEquals(tokens[13], "i32");
        
        AssertTokenIsOfKind(tokens.Last(), TokenKind.Eof);
    }

    private static void AssertTokenIsOfKind(Token token, TokenKind expectedTokenKind)
    {
        Assert.Equal(expectedTokenKind, token.Kind);
    }

    private static void AssertLexemeEquals(Token token, string expectedLexeme)
    {
        Assert.Equal(expectedLexeme, token.Lexeme);
    }
}
