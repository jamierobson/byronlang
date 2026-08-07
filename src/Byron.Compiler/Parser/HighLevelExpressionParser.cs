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
    
    private ExpressionNode ParseExpression()
    {
        var expression = ParseBinaryExpression(0);

        if (ConsumingActiveTokenMatch(TokenKind.OnError))
        {
            var fallback = ParsePrimaryExpression();
            return new OnErrorExpressionNode(expression, fallback, new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, fallback.Span.End));
        }
        if (ConsumingActiveTokenMatch(TokenKind.QuestionMark))
        {
            var operationToken = Previous();
            return new BubbleError(expression, new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, operationToken.Span.End));
        }

        return expression;
    }
    
    private ExpressionNode ParseBinaryExpression(int minPrecedence)
    {
        var expression = ParseUnary(); //todo: Where does this go?
        
        // var expression = ParsePrimaryExpression();

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
            ExpressionNode expression;
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
                var endToken = Consume(TokenKind.RParen, "Expected ')'.");
                expression = new CallExpressionNode(
                    new VariableExpressionNode(identifier.Lexeme, identifier.Span), arguments, 
                    new SourceSpan(identifier.Span.Line, identifier.Span.Column, identifier.Span.Start, endToken.Span.End));
            }
            else if (ConsumingActiveTokenMatch(TokenKind.LBrace))
            {
                var initializers = ParseStructFieldInitializers();
                var endToken = Consume(TokenKind.RBrace, "Expected '}' after struct field initialization.");

                var typeNode = new NominalTypeNode(identifier.Lexeme, [], identifier.Span);

                expression = new StructFieldInitializationExpressionNode(typeNode, initializers,
                    identifier.Span with { End = endToken.Span.End });
            }
            else
            {
                expression = new VariableExpressionNode(identifier.Lexeme, identifier.Span);
            }

            return ParsePostfixExpression(expression);
        }
        
        throw new ByronHighLevelParserException($"Parsing failed on token {Peek().Lexeme} at {Peek().Span}" + Peek().Lexeme, Peek().Span);
    }
    
    private ExpressionNode ParsePostfixExpression(ExpressionNode expression)
    {
        while (ConsumingActiveTokenMatch(TokenKind.Dot))
        {
            var memberToken = Consume(TokenKind.Identifier, "Expected field or member name after '.'.");
            expression = new MemberAccessExpressionNode(
                expression, 
                memberToken.Lexeme, 
                expression.Span with { End = memberToken.Span.End }
            );
        }
        return expression;
    }
    
    private List<StructFieldInitializerNode> ParseStructFieldInitializers()
    {
        var initializers = new List<StructFieldInitializerNode>();

        while (!ActiveTokenMatch(TokenKind.RBrace))
        {
            var nameToken = Consume(TokenKind.Identifier, "Expected field name in struct initialization.");
            Consume(TokenKind.Colon, "Expected ':' after field name.");
        
            var fieldValueExpression = ParseExpression();
        
            initializers.Add(new StructFieldInitializerNode(nameToken.Lexeme, fieldValueExpression, nameToken.Span with { End = fieldValueExpression.Span.End }) );

            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }

            Consume(TokenKind.Comma, "Expected ',' or '}' in struct field initializers.");
        }

        return initializers;
    }
    
    private ExpressionNode ParseUnary()
    {
        if (ConsumingActiveTokenMatch(TokenKind.Minus))
        {
            var operand = ParseUnary();

            if (operand is IntegerLiteralNode integerLiteral)
            {
                return new IntegerLiteralNode(-integerLiteral.Value, Previous().Span with { End = integerLiteral.Span.End });
            }

            // Fold -<float> into FloatLiteralNode
            // if (operand is literalnode floatLit)
            // {
            //     return new FloatLiteralNode(-floatLit.Value, CombineSpans(operationSpan, floatLit.Span));
            // }

            return new UnaryExpressionNode(UnaryOperator.Negative, operand, Previous().Span with { End = operand.Span.End });
        }

        if (ConsumingActiveTokenMatch(TokenKind.Bang))
        {
            var operand = ParseUnary();

            if (operand is BoolLiteralNode booleanLiteral)
            {
                return new BoolLiteralNode(!booleanLiteral.Value, Previous().Span with { End = booleanLiteral.Span.End });
            }

            return new UnaryExpressionNode(UnaryOperator.Not, operand, Previous().Span with { End = operand.Span.End });
        }

        return ParsePrimaryExpression();
    }
}