using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.Parser;

public record ScopeContext(ImplementBlockDeclarationNode? ImplementBlock)
{
    public static ScopeContext Global => new((ImplementBlockDeclarationNode?)null);
}

public partial class ByronHighLevelAstParser
{
    private BlockStatementNode ParseBlockStatement(ScopeContext context)
    {
        var open = Consume(TokenKind.LBrace, "Expected '{'.");
        var statements = new List<StatementNode>();
        while (!ActiveTokenMatch(TokenKind.RBrace))
        {
            if (ConsumingActiveTokenMatch(TokenKind.BlockComment) || ConsumingActiveTokenMatch(TokenKind.DocComment) ||
                ConsumingActiveTokenMatch(TokenKind.LineComment))
            {
                continue;
            }
            statements.Add(ParseStatement(context));
        }

        var close = Consume(TokenKind.RBrace, "Expected '}'.");
        return new BlockStatementNode(statements, new SourceSpan(open.Span.Line, open.Span.Column, open.Span.Start, close.Span.End));
    }

    private StatementNode ParseStatement(ScopeContext context)
    {
        if (ConsumingActiveTokenMatch(TokenKind.If))
        {
            return ParseIfStatement(context);
        }
        
        if (ConsumingActiveTokenMatch(TokenKind.Return))
        {
            var start = Previous();
            ExpressionNode? expr = null;
            
            if (!ActiveTokenMatch(TokenKind.Semicolon))
            {
                expr = ParseExpression(context);
            }

            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new ReturnStatementNode(expr, new SourceSpan(start.Span.Line, start.Span.Column, start.Span.Start, semiColon.Span.End));
        }
        if (ConsumingActiveTokenMatch(TokenKind.Break))
        {
            var start = Previous();
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new BreakStatement(ExpandSpan(start, semiColon));
        }
        if (ConsumingActiveTokenMatch(TokenKind.Continue))
        {
            var start = Previous();
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new ContinueStatement(ExpandSpan(start, semiColon));
        }
        if (ConsumingActiveTokenMatch(TokenKind.While))
        {
            return ParseWhileLoopStatement(context);
        }
        
        if (ConsumingActiveTokenMatch(TokenKind.Let) || ConsumingActiveTokenMatch(TokenKind.Var))
        {
            var mutabilityToken = Previous();
            var isMutable = mutabilityToken is {Kind: TokenKind.Var};
            var nameToken = Consume(TokenKind.Identifier, "Expected variable name.");
            TypeNode? type = null;
            if (ConsumingActiveTokenMatch(TokenKind.Colon))
            {
                type = ParseTypeSignature(context, nameToken); 
            }
            Consume(TokenKind.Equals, "Expected '='.");
            
            var initializer = ParseExpression(context);
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new VariableDeclarationNode(isMutable, nameToken.Lexeme, type, initializer, new SourceSpan(mutabilityToken.Span.Line, mutabilityToken.Span.Column, mutabilityToken.Span.Start, semiColon.Span.End));
        }

        var freeExpression = ParseExpression(context);

        if (ConsumingActiveTokenMatch(TokenKind.Equals))
        {
            var value = ParseExpression(context);
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';' after assignment.");
            return new AssignmentStatementNode(freeExpression, value, ExpandSpan(value, semiColon));
        }

        if (ConsumingActiveTokenMatch(TokenKind.Semicolon))
        {
            return new ExpressionStatementNode(freeExpression, ExpandSpan(freeExpression, Previous()));
        }
        
        throw new ByronNotImplementedException("Fallback basic statements", this, Previous().Span);
    }
    
    private IfElseStatement ParseIfStatement(ScopeContext context)
    {
        var ifToken = Previous();
        Consume(TokenKind.LParen, "Expected '(' after 'if'.");
        var condition = ParseExpression(context);
        Consume(TokenKind.RParen, "Expected ')' after condition.");

        var thenBranch = ParseBlockStatement(context);
        var span = ifToken.Span;
        
        BlockStatementNode? elseBranch = null;
        
        if (ConsumingActiveTokenMatch(TokenKind.Else))
        {
            elseBranch = ParseBlockStatement(context);
            span = ExpandSpan(ifToken, elseBranch);   
        }
        
        return new IfElseStatement(condition, thenBranch, elseBranch, span);
    }

    private WhileStatement ParseWhileLoopStatement(ScopeContext context)
    {
        var whileToken = Previous();
        Consume(TokenKind.LParen, "Expected '(' after 'while'.");
        var condition = ParseExpression(context);
        Consume(TokenKind.RParen, "Expected ')' after condition.");
        
        var body = ParseBlockStatement(context);
            
        return new WhileStatement(condition, body, ExpandSpan(whileToken, body));
    } 
}