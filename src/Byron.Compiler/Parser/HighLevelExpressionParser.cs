using System.Globalization;
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
    
    private ExpressionNode ParseExpression(SelfTypeContext? self)
    {
        var expression = PrattParseBinaryExpression(self, 0);
        
        if (ConsumingActiveTokenMatch(TokenKind.OnError))
        {
            var fallback = ParsePrimaryExpression(self);
            return new OnErrorExpressionNode(expression, fallback, new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, fallback.Span.End));
        }
        if (ConsumingActiveTokenMatch(TokenKind.QuestionMark))
        {
            var operationToken = Previous();
            return new BubbleError(expression, new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, operationToken.Span.End));
        }

        return expression;
    }

    private ExpressionNode PrattParseBinaryExpression(SelfTypeContext? self, int minPrecedence)
    {
        var expression = ParseUnary(self);

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
        
            var rightSide = PrattParseBinaryExpression(self, precedence + 1);
        
            expression = new BinaryExpressionNode(
                expression, 
                maybeBinaryOperator.Value, 
                rightSide,
                new SourceSpan(expression.Span.Line, expression.Span.Column, expression.Span.Start, rightSide.Span.End)
            );
        }

        return expression;
    }
    
    private ExpressionNode ParseUnary(SelfTypeContext? self)
    {
        if (ConsumingActiveTokenMatch(TokenKind.Minus))
        {
            var operand = ParseUnary(self);

            if (operand is IntegerLiteralNode integerLiteral)
            {
                return new IntegerLiteralNode(-integerLiteral.Value, ExpandSpan(Previous(), integerLiteral));
            }

            if (operand is FloatLiteralNode floatLiteral)
            {
                return new FloatLiteralNode(-floatLiteral.Value, ExpandSpan(Previous(), floatLiteral));
            }

            return new UnaryExpressionNode(UnaryOperator.Negative, operand, ExpandSpan(Previous(), operand));
        }

        if (ConsumingActiveTokenMatch(TokenKind.Bang))
        {
            var operand = ParseUnary(self);

            if (operand is BooleanLiteralNode booleanLiteral)
            {
                return new BooleanLiteralNode(!booleanLiteral.Value, ExpandSpan(Previous(), booleanLiteral));
            }

            return new UnaryExpressionNode(UnaryOperator.Not, operand, ExpandSpan(Previous(), operand));
        }

        if (ConsumingActiveTokenMatch(TokenKind.Ampersand))
        {
            var ampersand = Previous();
            var isMutable = ConsumingActiveTokenMatch(TokenKind.Var);
            var targetExpression = ParsePrimaryExpression(self);
            
            return new AddressOfExpressionNode(targetExpression, isMutable, ExpandSpan(ampersand, targetExpression));
        }

        return ParsePostfixExpression(self);
    }
    
    private ExpressionNode ParsePostfixExpression(SelfTypeContext? self)
    {
        var expression = ParsePrimaryExpression(self);
        
        while (!IsAtEnd())
        { 
            if (ConsumingActiveTokenMatch(TokenKind.LParen))
            {
                expression = ParseCallExpression(self, expression);
                continue;
            }
            
            if (ConsumingActiveTokenMatch(TokenKind.Dot))
            {
                if (ConsumingActiveTokenMatch(TokenKind.Asterisk))
                {
                    expression = new DereferenceExpressionNode(expression, ExpandSpan(expression, Peek()));
                    continue;
                }
            
                var memberToken = Consume(TokenKind.Identifier, "Expected field or member name after '.'.");
                expression = new MemberAccessExpressionNode(
                    expression, 
                    memberToken.Lexeme, 
                    ExpandSpan(expression, memberToken)
                );
                continue;
            }
            break;
        }
        
        return expression;
    }

    private ExpressionNode ParsePrimaryExpression(SelfTypeContext? self)
    {
        if (ConsumingActiveTokenMatch(TokenKind.LParen))
        {
            var expression = ParseExpression(self);
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
            return new BooleanLiteralNode(true, Previous().Span);
        }
        if (ConsumingActiveTokenMatch(TokenKind.False))
        {
            return new BooleanLiteralNode(false, Previous().Span);
        }

        if (ConsumingActiveTokenMatch(TokenKind.SelfType))
        {
            var selfToken = Previous();
            
            if (self is null)
            {
                throw new ByronHighLevelParserException(selfToken);
            }
            
            _ = Consume(TokenKind.LBrace, "Expected '{' in struct field initialization.");
            var initializers = ParseStructFieldInitializers(self);
            var endToken = Consume(TokenKind.RBrace, "Expected '}' after struct field initialization.");

            var selfType = self.GetSelfType(selfToken.Span);
            if (selfType is not NominalTypeNode nominalType)
            {
                throw new ByronHighLevelParserException($"Invalid struct initialization, 'Self' type is resolved as {selfType.Symbol}, which isn't a valid target for struct initialization ", selfToken.Span);
            }
            
            return new StructFieldInitializationExpressionNode(nominalType, initializers, ExpandSpan(selfToken, endToken));
        }
        if (ConsumingActiveTokenMatch(TokenKind.SelfReceiver))
        {
            var selfToken = Previous();
            
            if (self is null)
            {
                throw new ByronHighLevelParserException(selfToken);
            }
            
            return new VariableExpressionNode(selfToken.Lexeme, selfToken.Span);
            
        }

        if (ActiveTokenMatch(TokenKind.Identifier))
        {
            ExpressionNode expression;
            var identifierChain = ParseMultiSegmentIdentifier("in a multi segmented identifier string");

            if (ConsumingActiveTokenMatch(TokenKind.LBrace))
            {
                var initializers = ParseStructFieldInitializers(self);
                var endToken = Consume(TokenKind.RBrace, "Expected '}' after struct field initialization.");
                var typeNode = new NominalTypeNode(identifierChain.Segments, identifierChain.Span);

                expression = new StructFieldInitializationExpressionNode(typeNode, initializers,
                    ExpandSpan(identifierChain.Span, endToken.Span));
            }
            else
            {
                expression = new VariableExpressionNode(identifierChain.Segments[0], identifierChain.Span);

                for (var i = 1; i <= identifierChain.Segments.Length - 1; i++)
                {
                    expression = new MemberAccessExpressionNode(
                        expression,
                        identifierChain.Segments[i],
                        identifierChain.Span
                    ); 
                }
            }

            return expression;
        }
        
        throw new ByronHighLevelParserException($"Parsing failed on token {Peek().Lexeme} at {Peek().Span}" + Peek().Lexeme, Peek().Span);
    }

    private CallExpressionNode ParseCallExpression(SelfTypeContext? self, ExpressionNode callee)
    {
        var arguments = new List<ExpressionNode>();
        if (!ActiveTokenMatch(TokenKind.RParen))
        {
            do
            {
                arguments.Add(ParseExpression(self));
            } while (ConsumingActiveTokenMatch(TokenKind.Comma));
        }
        var endToken = Consume(TokenKind.RParen, "Expected ')'.");
        return new FreeFunctionCallExpressionNode(callee, arguments, ExpandSpan(callee, endToken));
    }

    private List<StructFieldInitializerNode> ParseStructFieldInitializers(SelfTypeContext? self)
    {
        var initializers = new List<StructFieldInitializerNode>();

        while (!ActiveTokenMatch(TokenKind.RBrace))
        {
            var nameToken = Consume(TokenKind.Identifier, "Expected field name in struct initialization."); 
            Consume(TokenKind.Colon, "Expected ':' after field name.");
        
            var fieldValueExpression = ParseExpression(self);
        
            initializers.Add(new StructFieldInitializerNode(nameToken.Lexeme, fieldValueExpression, ExpandSpan(nameToken, fieldValueExpression)));

            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }

            Consume(TokenKind.Comma, "Expected ',' or '}' in struct field initializers.");
        }

        return initializers;
    }
}