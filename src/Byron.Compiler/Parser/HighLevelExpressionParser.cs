using Byron.Compiler.AST;
using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;

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
}