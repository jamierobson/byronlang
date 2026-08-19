using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.AST;
using Byron.Compiler.Exceptions;
using Byron.Compiler.Lexer;
using Byron.Compiler.Parser;

namespace Byron.Compiler.Tests;

public class FunctionArgumentParserTests
{
    private static Token ToToken(TokenKind kind, string lexeme)
    {
        return Token.Create(kind, lexeme, SourceSpan.Empty);        
    }

    private static SelfTypeContext ImplementBlock() => SelfTypeContext.From(new ImplementBlockDeclarationNode(new("name", SourceSpan.Empty), null, SourceSpan.Empty));
    
    private List<Token> CreateFunctionArgumentTokenStream(params IEnumerable<(TokenKind kind, string lexeme)> definitions)
    {
        var tokens = new List<Token> {ToToken(TokenKind.LParen, "(")};
        tokens.AddRange(definitions.Select(d => ToToken(d.kind, d.lexeme)));
        tokens.Add(ToToken(TokenKind.RParen, ")"));
        tokens.Add(ToToken(TokenKind.Eof, ""));
        
        return tokens;
    }

    [Fact]
    public void Parse_WithSelf_WhenSelfDisallowed_Throws()
    {        
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.SelfReceiver, "self"),
            (TokenKind.Colon, ":"),
            (TokenKind.Ampersand, "&"),
            (TokenKind.SelfType, "Self")
        ); // (self: Self)
        
        // Act + Assert
        Assert.Throws<ByronHighLevelParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None));
    }

    [Fact]
    public void Parse_WithSelfInSecondPosition_WhenSelfAllowed_Throws()
    {
        var tokenStream =  CreateFunctionArgumentTokenStream(  
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32"),
            (TokenKind.Comma, ","),     
            (TokenKind.SelfReceiver, "self"),
            (TokenKind.Colon, ":"),
            (TokenKind.Ampersand, "&"),
            (TokenKind.SelfType, "Self")
        );
        
        // Act + Assert
        Assert.Throws<ByronHighLevelParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None));
    }

    [Fact]
    public void Parse_WithSelfInFirstPosition_WhenSelfAllowed_ReturnsExpected()
    {        
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.SelfReceiver, "self"),
            (TokenKind.Colon, ":"),
            (TokenKind.Ampersand, "&"),
            (TokenKind.SelfType, "Self")
        ); // (self: &Self)
        
        // Act + Assert
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(ImplementBlock());
        
        Assert.Single(result);
        var argument = result.Single();
        Assert.Equal("self", argument.Name);
    }
    
    [Fact]
    public void Parse_WithEmptyArguments_ReturnsEmptyList()
    {
        // Arrange
        var tokenStream = CreateFunctionArgumentTokenStream(); //()

        // Act
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None);

        // Assert
        Assert.Empty(result);
    }
    
    [Fact]
    public void Parse_WithNoClosingBrace_Throws()
    {
        // Arrange
        List<Token> tokenStream = [ ToToken(TokenKind.LParen, "(") ]; // (
    
        // Act + Assert
        Assert.Throws<ByronHighLevelParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None));
    }
    
    [Fact]
    public void Parse_SinglePrimitiveArgument_CreatesExpected()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32")
        ); // (x: i32)
    
        // Act
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None);
    
        // Assert
        Assert.Single(result);
        
        var argument = result.Single();
        Assert.Equal("x", argument.Name);
        Assert.Equal(ReceiverBindingOwnership.ImplicitCopy, argument.Ownership);
        Assert.Equal(typeof(Int32TypeNode), argument.Type.GetType());
    }
    
    [Fact]
    public void Parse_WithMissingSecondArgument_Throws()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32"),
            (TokenKind.Comma, ",")
        ); // (x: i32,)
    
        // Act + Assert
        Assert.Throws<ByronHighLevelParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None));
    }
    
    [Fact]
    public void Parse_WithMalformedSecondArgument_MissingIdentifier_Throws()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32"),
            (TokenKind.Comma, ","),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32")
        ); // (x: i32, : i32)
    
        // Act + Assert
        Assert.Throws<ByronHighLevelParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None));
    }
    
    [Fact]
    public void Parse_WithMalformedSecondArgument_MissingType_Throws()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32"),
            (TokenKind.Comma, ","),
            (TokenKind.Identifier, "y"),
            (TokenKind.Colon, ":")
        ); // (x: i32, y:)
    
        // Act + Assert
        Assert.Throws<ByronHighLevelParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None));
    }
    
    [Fact]
    public void Parse_WithMalformedSecondArgument_MissingColon_Throws()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32"),
            (TokenKind.Comma, ","),
            (TokenKind.Identifier, "y"),
            (TokenKind.Identifier, "i32")
        ); // (x: i32, y i32)
    
        // Act + Assert
        Assert.Throws<ByronHighLevelParserException>(() => new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None));
    }
    
    [Fact]
    public void Parse_TwoPrimitiveArguments_CreatesExpected()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32"),
            (TokenKind.Comma, ","),
            (TokenKind.Identifier, "y"),
            (TokenKind.Colon, ":"),
            (TokenKind.Identifier, "i32")
        );
    
        // Act
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None);
    
        // Assert
        Assert.Equal(2, result.Count);
        
        var first = result[0];
        Assert.Equal("x", first.Name);
        Assert.Equal(ReceiverBindingOwnership.ImplicitCopy, first.Ownership);
        Assert.Equal(typeof(Int32TypeNode), first.Type.GetType());
        
        var second = result[1];
        Assert.Equal("y", second.Name);
        Assert.Equal(ReceiverBindingOwnership.ImplicitCopy, second.Ownership);
        Assert.Equal(typeof(Int32TypeNode), second.Type.GetType());
    }

    [Fact]
    public void Parse_MovedArgument_CreatesExpected()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Take, "take"),
            (TokenKind.Identifier, "i32")
        );
    
        // Act
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None);
    
        // Assert
        Assert.Single(result);
        
        var argument = result.Single();
        Assert.Equal("x", argument.Name);
        Assert.Equal(ReceiverBindingOwnership.Owned, argument.Ownership);
        Assert.Equal(typeof(Int32TypeNode), argument.Type.GetType());
    }

    [Fact]
    public void Parse_ImmutableBorrowArgument_CreatesExpected()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Ampersand, "&"),
            (TokenKind.Identifier, "i32")
        );
    
        // Act
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None);
    
        // Assert
        Assert.Single(result);
        
        var argument = result.Single();
        Assert.Equal("x", argument.Name);
        Assert.Equal(ReceiverBindingOwnership.ImmutableBorrow, argument.Ownership);
        Assert.Equal(typeof(Int32TypeNode), argument.Type.GetType());
    }

    [Fact]
    public void Parse_MutableBorrowArgument_CreatesExpected()
    {
        // Arrange
        var tokenStream =  CreateFunctionArgumentTokenStream(       
            (TokenKind.Identifier, "x"),
            (TokenKind.Colon, ":"),
            (TokenKind.Ampersand, "&"),
            (TokenKind.Var, "var"),
            (TokenKind.Identifier, "i32")
        );
    
        // Act
        var result = new ByronHighLevelAstParser(tokenStream).ParseFunctionParameters(SelfTypeContext.None);
    
        // Assert
        Assert.Single(result);
        
        var argument = result.Single();
        Assert.Equal("x", argument.Name);
        Assert.Equal(ReceiverBindingOwnership.MutableBorrow, argument.Ownership);
        Assert.Equal(typeof(Int32TypeNode), argument.Type.GetType());
    }
}