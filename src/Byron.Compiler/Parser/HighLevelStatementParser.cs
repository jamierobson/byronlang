using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.Parser;

public record SelfTypeContext(ImplementBlockDeclarationNode? ImplementBlock, TraitTypeNode? TraitDeclaration)
{
    public static SelfTypeContext None = new(null, null);
    public static SelfTypeContext From(ImplementBlockDeclarationNode block) => new(block, null);
    public static SelfTypeContext From(TraitTypeNode trait) => new(null, trait);
    public TypeNode GetSelfType(SourceSpan sourceSpan)
    {
        if (ImplementBlock is null && TraitDeclaration is null)
        {
            throw new ByronHighLevelParserException("The 'Self' type is only valid in an implementation block or in a trait function declaration", sourceSpan);
        }
        
        return ImplementBlock is not null
            ? ImplementBlock.TypeNode
            : TraitDeclaration!;
    }
}

public partial class ByronHighLevelAstParser
{
    private BlockStatementNode ParseBlockStatement(SelfTypeContext? self)
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
            statements.Add(ParseStatement(self));
        }

        var close = Consume(TokenKind.RBrace, "Expected '}'.");
        return new BlockStatementNode(statements, new SourceSpan(open.Span.Line, open.Span.Column, open.Span.Start, close.Span.End));
    }

    private StatementNode ParseStatement(SelfTypeContext? self)
    {
        if (ConsumingActiveTokenMatch(TokenKind.If))
        {
            return ParseIfStatement(self);
        }
        
        if (ConsumingActiveTokenMatch(TokenKind.Return))
        {
            var start = Previous();
            ExpressionNode? expression = null;
            
            if (!ActiveTokenMatch(TokenKind.Semicolon))
            {
                expression = ParseExpression(self);
            }

            var semiColon = Consume(TokenKind.Semicolon, "Expected ';' after return expression.");
            return new ReturnStatementNode(expression, new SourceSpan(start.Span.Line, start.Span.Column, start.Span.Start, semiColon.Span.End));
        }
        if (ConsumingActiveTokenMatch(TokenKind.Break))
        {
            var start = Previous();
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';' after 'break'.");
            return new BreakStatement(ExpandSpan(start, semiColon));
        }
        if (ConsumingActiveTokenMatch(TokenKind.Continue))
        {
            var start = Previous();
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';' after 'continue'.");
            return new ContinueStatement(ExpandSpan(start, semiColon));
        }
        if (ConsumingActiveTokenMatch(TokenKind.While))
        {
            return ParseWhileLoopStatement(self);
        }
        
        if (ConsumingActiveTokenMatch(TokenKind.Let) || ConsumingActiveTokenMatch(TokenKind.Var))
        {
            var mutabilityToken = Previous();
            var isMutable = mutabilityToken is {Kind: TokenKind.Var};
            var nameToken = Consume(TokenKind.Identifier, "Expected variable name.");
            TypeNode? type = null;
            if (ConsumingActiveTokenMatch(TokenKind.Colon))
            {
                type = ParseTypeSignature(self, nameToken); 
            }
            Consume(TokenKind.Equals, "Expected '='.");
            
            var initializer = ParseExpression(self);
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';'. after variable declaration assignment");
            return new VariableDeclarationNode(isMutable, nameToken.Lexeme, type, initializer, new SourceSpan(mutabilityToken.Span.Line, mutabilityToken.Span.Column, mutabilityToken.Span.Start, semiColon.Span.End));
        }

        var freeExpression = ParseExpression(self);

        if (ConsumingActiveTokenMatch(TokenKind.Equals))
        {
            var value = ParseExpression(self);
            var semiColon = Consume(TokenKind.Semicolon, "Expected ';' after assignment.");
            return new AssignmentStatementNode(freeExpression, value, ExpandSpan(value, semiColon));
        }

        if (ConsumingActiveTokenMatch(TokenKind.Semicolon))
        {
            return new ExpressionStatementNode(freeExpression, ExpandSpan(freeExpression, Previous()));
        }
        
        throw new ByronNotImplementedException("Fallback basic statements", this, Previous().Span);
    }
    
    private IfElseStatement ParseIfStatement(SelfTypeContext? self)
    {
        var ifToken = Previous();
        Consume(TokenKind.LParen, "Expected '(' after 'if'.");
        var condition = ParseExpression(self);
        Consume(TokenKind.RParen, "Expected ')' after condition.");

        var thenBranch = ParseBlockStatement(self);
        var span = ifToken.Span;
        
        BlockStatementNode? elseBranch = null;
        
        if (ConsumingActiveTokenMatch(TokenKind.Else))
        {
            elseBranch = ParseBlockStatement(self);
            span = ExpandSpan(ifToken, elseBranch);   
        }
        
        return new IfElseStatement(condition, thenBranch, elseBranch, span);
    }

    private WhileStatement ParseWhileLoopStatement(SelfTypeContext? self)
    {
        var whileToken = Previous();
        Consume(TokenKind.LParen, "Expected '(' after 'while'.");
        var condition = ParseExpression(self);
        Consume(TokenKind.RParen, "Expected ')' after condition.");
        
        var body = ParseBlockStatement(self);
            
        return new WhileStatement(condition, body, ExpandSpan(whileToken, body));
    } 
}