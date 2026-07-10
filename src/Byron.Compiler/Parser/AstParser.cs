using Byron.Compiler.AST;
using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.Parser;

// TODO: Split this out into partial files (Declarations, Statements, Expressions) when it gets unwieldy.
public class AstParser
{
    private readonly List<Token> _tokens;
    private int _activeTokenIndex = 0;

    public AstParser(List<Token> tokens) => _tokens = tokens;

    public ProgramNode Parse()
    {
        var functions = new List<FunctionDeclarationNode>();
        while (!IsAtEnd())
        {
            functions.Add(ParseFunctionDeclaration());
        }

        return new ProgramNode([..functions]);
    }

    private FunctionDeclarationNode ParseFunctionDeclaration()
    {
        var fnToken = Consume(TokenKind.Fn, "Expected 'fn'.");
        var nameToken = Consume(TokenKind.Identifier, "Expected function name.");

        var parameters = ParseFunctionArguments(); 
        _ = Consume(TokenKind.Colon, "Expected ':'.");
        var returnType = ParseTypeSignature();
        var body = ParseBlockStatement();

        return new FunctionDeclarationNode(nameToken.Lexeme, parameters, returnType, body, new SourceSpan(fnToken.Span.Line, fnToken.Span.Column, fnToken.Span.Start, body.Span.End));
    }

    public List<ParameterNode> ParseFunctionArguments()
    {   
        _ = Consume(TokenKind.LParen, "Expected '('.");
        var parameters = new List<ParameterNode>();
        if (!ActiveTokenMatch(TokenKind.RParen))
        {
            do
            {
                var parameterName = Consume(TokenKind.Identifier, "Expected parameter name.");
                _ = Consume(TokenKind.Colon, "Expected ':'.");

                ReceiverBindingOwnership receiverBindingOwnership;
                
                if (ConsumingActiveTokenMatch(TokenKind.Take))
                {
                    receiverBindingOwnership = ReceiverBindingOwnership.Owned;
                }
                else if (Peek() is { Kind :TokenKind.Identifier})
                {
                    receiverBindingOwnership = ReceiverBindingOwnership.ImplicitCopy;
                } 
                else if (ConsumingActiveTokenMatch(TokenKind.Ampersand))
                {
                    receiverBindingOwnership = ConsumingActiveTokenMatch(TokenKind.Var) 
                        ? ReceiverBindingOwnership.MutableBorrow 
                        : ReceiverBindingOwnership.ImmutableBorrow;
                }
                else
                {
                    throw new ByronParserException(Peek());
                }
                
                var parameterType = ParseTypeSignature();
                parameters.Add(new ParameterNode(receiverBindingOwnership, parameterName.Lexeme, parameterType, new SourceSpan(parameterName.Span.Line, parameterName.Span.Column, parameterName.Span.Start, parameterType.Span.End)));
            } while (ConsumingActiveTokenMatch(TokenKind.Comma));
        }

        _ = Consume(TokenKind.RParen, "Expected ')'.");
        return parameters;
    }

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
            var init = ParseExpression();
            var semi = Consume(TokenKind.Semicolon, "Expected ';'.");
            return new VariableDeclarationNode(isMutable, name.Lexeme, type, init, new SourceSpan(mutabilityToken.Span.Line, mutabilityToken.Span.Column, mutabilityToken.Span.Start, semi.Span.End));
        }
        throw new NotImplementedException("Fallback basic statements not implemented.");
    }

    private ExpressionNode ParseExpression()
    {
        var expression = ParsePrimaryExpression();

        // Handle post-fix syntax sugar operations (onerror, ?)
        if (ConsumingActiveTokenMatch(TokenKind.OnError))
        {
            _ = Previous();
            var fallback = ParsePrimaryExpression();
            return new OnErrorExpressionNode(expression, fallback, new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, fallback.Span.End));
        }
        if (ConsumingActiveTokenMatch(TokenKind.QuestionMark))
        {
            var operationToken = Previous();
            return new TryOperatorExpressionNode(expression, new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, operationToken.Span.End));
        }
        return expression;
    }

    private ExpressionNode ParsePrimaryExpression()
    {
        if (ConsumingActiveTokenMatch(TokenKind.IntLiteral))
        {
            return new IntegerLiteralNode(Convert.ToInt64(Previous().Lexeme), Previous().Span);
        }
        if (ConsumingActiveTokenMatch(TokenKind.Identifier))
        {
            var identifier = Previous();
            if (ConsumingActiveTokenMatch(TokenKind.LParen))
            {
                var arguments = new List<ExpressionNode>();
                if (!ActiveTokenMatch(TokenKind.RParen))
                {
                    do
                    {
                        arguments.Add(ParseExpression());
                    } while (ConsumingActiveTokenMatch(TokenKind.Comma));
                }
                var rparen = Consume(TokenKind.RParen, "Expected ')'.");
                return new CallExpressionNode(new VariableExpressionNode(identifier.Lexeme, identifier.Span), arguments, new SourceSpan(identifier.Span.Line, identifier.Span.Column, identifier.Span.Start, rparen.Span.End));
            }
            return new VariableExpressionNode(identifier.Lexeme, identifier.Span);
        }
        throw new ByronParserException("Parsing failed on token: " + Peek().Lexeme, Peek().Span);
    }

    private TypeNode ParseTypeSignature()
    {
        var token = Advance();
        return token.Lexeme switch
        {
            "i64" => new Int64TypeNode(token.Span),
            "i32" => new Int32TypeNode(token.Span),
            "void" => new VoidTypeNode(token.Span),
            _ => throw new ByronParserException($"Unknown type signature target: {token.Lexeme}", token.Span)
        };
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _activeTokenIndex++;
        return Previous();
    }

    private bool ConsumingActiveTokenMatch(TokenKind kind)
    {
        if (ActiveTokenMatch(kind))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool ActiveTokenMatch(TokenKind kind) => !IsAtEnd() && Peek().Kind == kind;
    private Token Peek() => _tokens[_activeTokenIndex];
    private Token Previous() => _tokens[_activeTokenIndex - 1];
    private bool IsAtEnd() => _activeTokenIndex >= _tokens.Count || Peek().Kind == TokenKind.Eof;
    private Token Consume(TokenKind kind, string error) => ActiveTokenMatch(kind) ? Advance() : throw new ByronParserException(error, SourceSpan.Empty);
}