using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.Parser;

public partial class ByronHighLevelAstParser
{
    private BlockStatementNode ParseBlockStatement()
    {
        var open = Consume(TokenKind.LBrace, "Expected '{'.");
        var statements = new List<StatementNode>();
        while (!ActiveTokenMatch(TokenKind.RBrace) && !IsAtEnd())
        {
            statements.Add(ParseStatement());
        }

        var close = Consume(TokenKind.RBrace, "Expected '}'.");
        return new BlockStatementNode(statements, new SourceSpan(open.Span.Line, open.Span.Column, open.Span.Start, close.Span.End));
    }

    private StatementNode ParseStatement()
    {
        if (ConsumingActiveTokenMatch(TokenKind.If))
        {
            return ParseIfStatement();
        }
        
        if (ConsumingActiveTokenMatch(TokenKind.Return))
        {
            var start = Previous();
            ExpressionNode? expr = null;
            
            if (!ActiveTokenMatch(TokenKind.Semicolon))
            {
                expr = ParseExpression();
            }

            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new ReturnStatementNode(expr, new SourceSpan(start.Span.Line, start.Span.Column, start.Span.Start, semiColon.Span.End));
        }
        
        if (ConsumingActiveTokenMatch(TokenKind.Let) || ConsumingActiveTokenMatch(TokenKind.Var))
        {
            var mutabilityToken = Previous();
            var isMutable = mutabilityToken is {Kind: TokenKind.Var};
            var name = Consume(TokenKind.Identifier, "Expected variable name.");
            TypeNode? type = null;
            if (ConsumingActiveTokenMatch(TokenKind.Colon)) { type = ParseTypeSignature(); }
            Consume(TokenKind.Equals, "Expected '='.");
            var initializer = ParseExpression();
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new VariableDeclarationNode(isMutable, name.Lexeme, type, initializer, new SourceSpan(mutabilityToken.Span.Line, mutabilityToken.Span.Column, mutabilityToken.Span.Start, semiColon.Span.End));
        }

        throw new ByronNotImplementedException("Fallback basic statements", this);
    }
    
    private IfElseStatement ParseIfStatement()
    {
        var ifToken = Previous();
        Consume(TokenKind.LParen, "Expected '(' after 'if'.");
        var condition = ParseExpression();
        Consume(TokenKind.RParen, "Expected ')' after condition.");

        var thenBranch = ParseBlockStatement();
        var span = ifToken.Span;
        
        BlockStatementNode? elseBranch = null;
        
        if (ConsumingActiveTokenMatch(TokenKind.Else))
        {
            elseBranch = ParseBlockStatement();
            span = ifToken.Span with { End = elseBranch.Span.End };
        }
        
        return new IfElseStatement(condition, thenBranch, elseBranch, span);
    }
}