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
        while (!ActiveTokenMatch(TokenKind.RBrace))
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
        if (ConsumingActiveTokenMatch(TokenKind.Break))
        {
            var start = Previous();
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new BreakStatement(start.Span with {End = semiColon.Span.End} );
        }
        if (ConsumingActiveTokenMatch(TokenKind.Continue))
        {
            var start = Previous();
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new ContinueStatement(start.Span with {End = semiColon.Span.End} );
        }
        if (ConsumingActiveTokenMatch(TokenKind.While))
        {
            return ParseWhileLoopStatement();
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

        var freeExpression = ParseExpression();

        if (ConsumingActiveTokenMatch(TokenKind.Equals))
        {
            var value = ParseExpression();
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';' after assignment.");
            return new AssignmentStatementNode(freeExpression, value, value.Span with {End = semiColon.Span.End});
        }

        if (ConsumingActiveTokenMatch(TokenKind.Semicolon))
        {
            return new ExpressionStatementNode(freeExpression, freeExpression.Span with { End = Previous().Span.End });
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

    private WhileStatement ParseWhileLoopStatement()
    {
        var whileSpan = Previous().Span;
        Consume(TokenKind.LParen, "Expected '(' after 'while'.");
        var condition = ParseExpression();
        Consume(TokenKind.RParen, "Expected ')' after condition.");
        
        var body = ParseBlockStatement();
            
        return new WhileStatement(condition, body, whileSpan with{ End = body.Span.End });
    } 
}