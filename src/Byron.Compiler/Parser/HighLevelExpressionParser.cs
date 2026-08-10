using System.Globalization;
using Byron.Compiler.AST;
using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.Parser;

public partial class ByronHighLevelAstParser
{
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
    
    private ExpressionNode ParseExpression(ScopeContext context)
    {
        var expression = ParseBinaryExpression(context, 0);

        if (ConsumingActiveTokenMatch(TokenKind.OnError))
        {
            var fallback = ParsePrimaryExpression(context);
            return new OnErrorExpressionNode(expression, fallback, new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, fallback.Span.End));
        }
        if (ConsumingActiveTokenMatch(TokenKind.QuestionMark))
        {
            var operationToken = Previous();
            return new BubbleError(expression, new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, operationToken.Span.End));
        }

        return expression;
    }
    
    private ExpressionNode ParseBinaryExpression(ScopeContext context, int minPrecedence)
    {
        var expression = ParseUnary(context);

        while (!IsAtEnd())
        {
            var followingToken = Peek();
            var maybeBinaryOperator = followingToken.Kind.ToBinaryOperator();

            if (maybeBinaryOperator is null)
            {
                break;
            }

            var precedence = GetOperatorPrecedence(maybeBinaryOperator.Value);
            if(expression is BinaryExpressionNode binaryOperator && minPrecedence == BitwiseOperationPrecedence && precedence == BitwiseOperationPrecedence && maybeBinaryOperator.Value != binaryOperator.Operator)
            {
                throw new ByronHighLevelParserException("Brackets requried when chaining bitwise operations", Peek().Span);
            }

            if (precedence < minPrecedence)
            {
                break;
            }

            Advance(); 
        
            var rightSide = ParseBinaryExpression(context, precedence + 1);
        
            expression = new BinaryExpressionNode(
                expression, 
                maybeBinaryOperator.Value, 
                rightSide,
                new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, rightSide.Span.End)
            );
        }

        return expression;
    }

    private ExpressionNode ParsePrimaryExpression(ScopeContext context)
    {
        if (ConsumingActiveTokenMatch(TokenKind.LParen))
        {
            var expression = ParseExpression(context);
            _ = Consume(TokenKind.RParen, "Expected closing parenthesis ')'");

            return expression;
        }
        if (ConsumingActiveTokenMatch(TokenKind.IntLiteral))
        {
            return new IntegerLiteralNode(Convert.ToInt64(Previous().Lexeme), Previous().Span);
        }

        if (ConsumingActiveTokenMatch(TokenKind.FloatLiteral))
        {
            return new FloatLiteralNode(Convert.ToDouble(Previous().Lexeme, CultureInfo.InvariantCulture), Previous().Span);
        }
        if (ConsumingActiveTokenMatch(TokenKind.True))
        {
            return new BoolLiteralNode(true, Previous().Span);
        }
        if (ConsumingActiveTokenMatch(TokenKind.False))
        {
            return new BoolLiteralNode(false, Previous().Span);
        }

        if (ConsumingActiveTokenMatch(TokenKind.SelfType))
        {
            var selfToken = Previous();
            
            if (context.ImplementBlock is null)
            {
                throw new ByronHighLevelParserException(selfToken);
            }
            
            _ = Consume(TokenKind.LBrace, "Expected '{' in struct field initialization.");
            var initializers = ParseStructFieldInitializers(context);
            var endToken = Consume(TokenKind.RBrace, "Expected '}' after struct field initialization.");

            var expression = new StructFieldInitializationExpressionNode(context.ImplementBlock.TypeNode, initializers, selfToken.Span with { End = endToken.Span.End });
            return ParsePostfixExpression(context, expression);
        }
        if (ConsumingActiveTokenMatch(TokenKind.SelfReceiver))
        {
            var selfToken = Previous();
            
            if (context.ImplementBlock is null)
            {
                throw new ByronHighLevelParserException(selfToken);
            }
            
            var expression = new VariableExpressionNode(selfToken.Lexeme, selfToken.Span);
            return ParsePostfixExpression(context, expression);
            
        }
        if (ConsumingActiveTokenMatch(TokenKind.Identifier))
        {
            var identifier = Previous();
            ExpressionNode expression;
            if (ConsumingActiveTokenMatch(TokenKind.LParen))
            {
                expression = ParseCallExpression(context, identifier);
            }
            else if (ConsumingActiveTokenMatch(TokenKind.LBrace))
            {
                var initializers = ParseStructFieldInitializers(context);
                var endToken = Consume(TokenKind.RBrace, "Expected '}' after struct field initialization.");

                var typeNode = new NominalTypeNode(identifier.Lexeme, [], identifier.Span);

                expression = new StructFieldInitializationExpressionNode(typeNode, initializers,
                    identifier.Span with { End = endToken.Span.End });
            }
            else
            {
                expression = new VariableExpressionNode(identifier.Lexeme, identifier.Span);
            }

            return ParsePostfixExpression(context, expression);
        }
        
        throw new ByronHighLevelParserException($"Parsing failed on token {Peek().Lexeme} at {Peek().Span}" + Peek().Lexeme, Peek().Span);
    }

    private CallExpressionNode ParseCallExpression(ScopeContext context, Token identifier)
    {
        var arguments = new List<ExpressionNode>();
        if (!ActiveTokenMatch(TokenKind.RParen))
        {
            do
            {
                arguments.Add(ParseExpression(context));
            } while (ConsumingActiveTokenMatch(TokenKind.Comma));
        }
        var endToken = Consume(TokenKind.RParen, "Expected ')'.");
        return new CallExpressionNode(
            new VariableExpressionNode(identifier.Lexeme, identifier.Span), arguments, 
            new SourceSpan(identifier.Span.Line, identifier.Span.Column, identifier.Span.Start, endToken.Span.End));
    }

    private ExpressionNode ParsePostfixExpression(ScopeContext context, ExpressionNode expression)
    {
        var identifier = Previous();
        while (!IsAtEnd())
        {
            if (ConsumingActiveTokenMatch(TokenKind.Dot))
            {
                if (ConsumingActiveTokenMatch(TokenKind.Asterisk))
                {
                    var dereferenceSpan = expression.Span with { End = Peek().Span.End };
                    expression = new DereferenceExpressionNode(expression, dereferenceSpan);
                    continue;
                }
            
                var memberToken = Consume(TokenKind.Identifier, "Expected field or member name after '.'.");
                expression = new MemberAccessExpressionNode(
                    expression, 
                    memberToken.Lexeme, 
                    expression.Span with { End = memberToken.Span.End }
                );
                continue;
            }

            if (ConsumingActiveTokenMatch(TokenKind.LParen))
            {
                if (identifier is not { Kind: TokenKind.Identifier })
                {
                    throw new ByronHighLevelParserException("Bad identifier token provided to parsing function invocation", Peek().Span);
                }
                expression = ParseCallExpression(context, identifier);
                continue;
            }
            break;
        }
        
        return expression;
    }

    private List<StructFieldInitializerNode> ParseStructFieldInitializers(ScopeContext context)
    {
        var initializers = new List<StructFieldInitializerNode>();

        while (!ActiveTokenMatch(TokenKind.RBrace))
        {
            var nameToken = Consume(TokenKind.Identifier, "Expected field name in struct initialization."); // 
            Consume(TokenKind.Colon, "Expected ':' after field name.");
        
            var fieldValueExpression = ParseExpression(context);
        
            initializers.Add(new StructFieldInitializerNode(nameToken.Lexeme, fieldValueExpression, nameToken.Span with { End = fieldValueExpression.Span.End }) );

            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }

            Consume(TokenKind.Comma, "Expected ',' or '}' in struct field initializers.");
        }

        return initializers;
    }
    
    private ExpressionNode ParseUnary(ScopeContext context)
    {
        if (ConsumingActiveTokenMatch(TokenKind.Minus))
        {
            var operand = ParseUnary(context);

            if (operand is IntegerLiteralNode integerLiteral)
            {
                return new IntegerLiteralNode(-integerLiteral.Value, Previous().Span with { End = integerLiteral.Span.End });
            }

            if (operand is FloatLiteralNode floatLiteral)
            {
                return new FloatLiteralNode(-floatLiteral.Value, Previous().Span with { End = floatLiteral.Span.End });
            }

            return new UnaryExpressionNode(UnaryOperator.Negative, operand, Previous().Span with { End = operand.Span.End });
        }

        if (ConsumingActiveTokenMatch(TokenKind.Bang))
        {
            var operand = ParseUnary(context);

            if (operand is BoolLiteralNode booleanLiteral)
            {
                return new BoolLiteralNode(!booleanLiteral.Value, Previous().Span with { End = booleanLiteral.Span.End });
            }

            return new UnaryExpressionNode(UnaryOperator.Not, operand, Previous().Span with { End = operand.Span.End });
        }

        return ParsePrimaryExpression(context);
    }
}