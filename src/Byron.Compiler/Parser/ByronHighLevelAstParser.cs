using Byron.Compiler.AST;
using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.Parser;

// TODO: Split this out into partial files (Declarations, Statements, Expressions) when it gets unwieldy.
public class ByronHighLevelAstParser
{
    private readonly List<Token> _tokens;
    private int _activeTokenIndex = 0;

    public ByronHighLevelAstParser(List<Token> tokens) => _tokens = tokens;

    public ProgramNode Parse()
    {
        var functions = new List<FunctionDeclarationNode>();
        while (!IsAtEnd())
        {
            functions.Add(ParseFunctionDeclaration());
        }

        return new ProgramNode([..functions]);
    }

    public FunctionDeclarationNode ParseFunctionDeclaration()
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
        // 1. Evaluate all math and binary operations first, starting from the absolute floor
        var expression = ParseBinaryExpression(0);

        // 2. Handle post-fix syntax sugar operations safely on the fully built math tree
        if (ConsumingActiveTokenMatch(TokenKind.OnError))
        {
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
    
    private ExpressionNode ParseBinaryExpression(int minPrecedence)
    {
        var expression = ParsePrimaryExpression();

        while (!IsAtEnd())
        {
            var followingToken = Peek();
            var maybeBinaryOperator = followingToken.Kind.ToBinaryOperator();

            if (maybeBinaryOperator is null)
                break;

            var precedence = GetOperatorPrecedence(maybeBinaryOperator.Value);
            if(expression is BinaryExpressionNode binaryOperator && minPrecedence == BitwiseOperationPrecedence && precedence == BitwiseOperationPrecedence && maybeBinaryOperator.Value != binaryOperator.Operator)
            {
                throw new ByronParserException("Brackets requried when chaining bitwise operations", Peek().Span);
            }

            if (precedence < minPrecedence)
            {
                break;
            }

            Advance(); 
        
            var rightSide = ParseBinaryExpression(precedence + 1);
        
            expression = new BinaryExpressionNode(
                expression, 
                maybeBinaryOperator.Value, 
                rightSide,
                new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, rightSide.Span.End)
            );
        }

        return expression;
    }
    

    private const short BitwiseOperationPrecedence = 5;
    
    private int GetOperatorPrecedence(BinaryOperator binaryOperator)
    {
        return binaryOperator switch {
            BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo => 8,
            BinaryOperator.Add or BinaryOperator.Subtract => 7,
            BinaryOperator.ShiftLeft or BinaryOperator.ShiftRight  => 6,
            BinaryOperator.BitwiseAnd or BinaryOperator.BitwiseOr or BinaryOperator.BitwiseXor=> BitwiseOperationPrecedence, // We do enforce bracketing for chaining bitwise operations in the parser 
            BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual => 4,
            BinaryOperator.Equal or BinaryOperator.NotEqual => 3,
            BinaryOperator.LogicalAnd => 2,
            BinaryOperator.LogicalOr => 1,
          _ => 0
        };
    }

    private ExpressionNode ParsePrimaryExpression()
    {
        if (ConsumingActiveTokenMatch(TokenKind.LParen))
        {
            var expression = ParseExpression();
            _ = Consume(TokenKind.RParen, "Expected closing parenthesis ')'");

            return expression;
        }
        if (ConsumingActiveTokenMatch(TokenKind.IntLiteral))
        {
            return new IntegerLiteralNode(Convert.ToInt64(Previous().Lexeme), Previous().Span);
        }
        if (ConsumingActiveTokenMatch(TokenKind.True))
        {
            return new BoolLiteralNode(true, Previous().Span);
        }
        if (ConsumingActiveTokenMatch(TokenKind.False))
        {
            return new BoolLiteralNode(false, Previous().Span);
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
            "boolean" => new BoolTypeNode(token.Span),
            "i8" => new Int8TypeNode(token.Span),
            "i16" => new Int16TypeNode(token.Span),
            "i32" => new Int32TypeNode(token.Span),
            "i64" => new Int64TypeNode(token.Span),
            "u8" => new UInt8TypeNode(token.Span),
            "u16" => new UInt16TypeNode(token.Span),
            "u32" => new UInt32TypeNode(token.Span),
            "u64" => new UInt64TypeNode(token.Span),
            "f32" => new Float32TypeNode(token.Span),
            "f64" => new Float64TypeNode(token.Span),
            "void" => new VoidTypeNode(token.Span),
            "unit" => new UnitTypeNode(token.Span),
            "rune" => new RuneTypeNode(token.Span),
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
    private Token PeekNext() => _tokens[_activeTokenIndex + 1];
    private bool IsAtEnd() => _activeTokenIndex >= _tokens.Count || Peek().Kind == TokenKind.Eof;
    private Token Consume(TokenKind kind, string error) => ActiveTokenMatch(kind) ? Advance() : throw new ByronParserException(error, SourceSpan.Empty);
}