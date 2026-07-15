using Byron.Compiler.AST.HighLevel;
using Xunit;
using Byron.Compiler.AST;
using Byron.Compiler.Lexer;
using Byron.Compiler.Parser;

namespace Byron.Compiler.Tests;

public class FunctionDeclarationParserTests
{
    private static Token ToToken(TokenKind kind, string lexeme)
    {
        return Token.Create(kind, lexeme, SourceSpan.Empty);        
    }

    private List<Token> CreateFunctionTokenStream(
        string functionName,
        List<(TokenKind kind, string lexeme)> parameterTokens,
        string returnTypeLexeme,
        List<(TokenKind kind, string lexeme)> bodyTokens)
    {
        List<(TokenKind kind, string lexeme)> tokenDefinitions = [
            (TokenKind.Fn, "fn"),
            (TokenKind.Identifier, functionName),
            (TokenKind.LParen, "("),
            ..parameterTokens,
            (TokenKind.RParen, ")"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, returnTypeLexeme),
            (TokenKind.LBrace, "{"),
            ..bodyTokens,
            (TokenKind.RBrace, "}"),
            (TokenKind.Eof, "")
        ];
        
        return tokenDefinitions.Select(x => ToToken(x.kind, x.lexeme)).ToList();
    }

    [Fact]
    public void Parse_MinimalVoidFunction_CreatesExpected()
    {
        // Arrange
        var tokenStream = CreateFunctionTokenStream(
            functionName: "foo",
            parameterTokens: [],
            returnTypeLexeme: "void",
            bodyTokens: []
        ); // fn foo(): void {}

        // Act
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionDeclaration();

        // Assert
        Assert.Equal("foo", result.Name);
        Assert.Empty(result.Parameters);
        Assert.Equal(typeof(VoidTypeNode), result.ReturnType.GetType());
        Assert.Empty(result.Body.Statements);
    }

    [Fact]
    public void Parse_FunctionWithMixedArgumentsAndReturn_CreatesExpectedAST()
    {
        // Arrange
        var parameters = new List<(TokenKind, string)>
        {
            (TokenKind.Identifier, "value"), (TokenKind.Colon, ":"), (TokenKind.Take, "take"), (TokenKind.Identifier, "i32"),
            (TokenKind.Comma, ","),
            (TokenKind.Identifier, "scale"), (TokenKind.Colon, ":"), (TokenKind.Ampersand, "&"), (TokenKind.Identifier, "i64")
        }; // fn calculate(value: take i32, scale: &i64): i32 { return 42; }

        var body = new List<(TokenKind, string)>
        {
            (TokenKind.Return, "return"), (TokenKind.IntLiteral, "42"), (TokenKind.Semicolon, ";")
        };

        var tokenStream = CreateFunctionTokenStream("calculate", parameters, "i32", body);

        // Act
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionDeclaration();

        // Assert
        Assert.Equal("calculate", result.Name);
        Assert.Equal(typeof(Int32TypeNode), result.ReturnType.GetType());

        Assert.Equal(2, result.Parameters.Count);
        Assert.Equal("value", result.Parameters[0].Name);
        Assert.Equal(ReceiverBindingOwnership.Owned, result.Parameters[0].Ownership);
        
        Assert.Equal("scale", result.Parameters[1].Name);
        Assert.Equal(ReceiverBindingOwnership.ImmutableBorrow, result.Parameters[1].Ownership);

        Assert.Single(result.Body.Statements);
        var returnStmt = Assert.IsType<ReturnStatementNode>(result.Body.Statements.Single());
        Assert.NotNull(returnStmt.Expression);
        Assert.IsType<IntegerLiteralNode>(returnStmt.Expression);
    }

    [Fact]
    public void Parse_MissingReturnTypeColon_Throws()
    {
        // Arrange: 
        var tokenStream = new List<(TokenKind kind, string lexeme)> {
            (TokenKind.Fn, "fn"),
            (TokenKind.Identifier, "badFunc"),
            (TokenKind.LParen, "("),
            (TokenKind.RParen, ")"),
            (TokenKind.Identifier, "void"),
            (TokenKind.LBrace, "{"),
            (TokenKind.RBrace, "}"),
            (TokenKind.Eof, "")
        }
        .Select(x => ToToken(x.kind, x.lexeme))
        .ToList(); // fn badFunc() void {}

        // Act + Assert
        Assert.Throws<ByronParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionDeclaration());
    }

    [Fact]
    public void Parse_MissingFunctionKeyword_Throws()
    {
        var tokenStream = new List<(TokenKind kind, string lexeme)> {
            (TokenKind.Identifier, "badFunc"),
            (TokenKind.LParen, "("),
            (TokenKind.RParen, ")"),
            (TokenKind.Colon, ":"),
            (TokenKind.Void, "void"),
            (TokenKind.LBrace, "{"),
            (TokenKind.RBrace, "}"),
            (TokenKind.Eof, "")
        }
        .Select(x => ToToken(x.kind, x.lexeme))
        .ToList(); // badFunc(): void {}

        // Act + Assert
        Assert.Throws<ByronParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionDeclaration() );
    }
}