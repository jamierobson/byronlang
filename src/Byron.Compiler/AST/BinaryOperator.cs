using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST;

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,

    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    ShiftLeft,
    ShiftRight,

    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,

    LogicalAnd,
    LogicalOr
}

public static partial class TokenKindExtensions
{
    extension(TokenKind tokenKind)
    {
        public BinaryOperator? ToBinaryOperator()
        {
            return tokenKind switch
            {
                // Arithmetic
                TokenKind.Plus           => BinaryOperator.Add,
                TokenKind.Minus          => BinaryOperator.Subtract,
                TokenKind.Star           => BinaryOperator.Multiply,
                TokenKind.Slash          => BinaryOperator.Divide,
                TokenKind.Percent        => BinaryOperator.Modulo,

                // Bitwise & Shifts
                TokenKind.Ampersand      => BinaryOperator.BitwiseAnd,
                TokenKind.Pipe           => BinaryOperator.BitwiseOr,
                TokenKind.Caret          => BinaryOperator.BitwiseXor,
                TokenKind.LAngleLAngle   => BinaryOperator.ShiftLeft,
                TokenKind.RAngleRAngle   => BinaryOperator.ShiftRight,

                // Relational Comparisons
                TokenKind.EqualsEquals    => BinaryOperator.Equal,
                TokenKind.BangEquals      => BinaryOperator.NotEqual,
                TokenKind.LAngle          => BinaryOperator.LessThan,
                TokenKind.LessEquals      => BinaryOperator.LessThanOrEqual,
                TokenKind.RAngle          => BinaryOperator.GreaterThan,
                TokenKind.GreaterEquals   => BinaryOperator.GreaterThanOrEqual,

                // Logical
                TokenKind.AmpersandAmpersand => BinaryOperator.LogicalAnd,
                TokenKind.PipePipe           => BinaryOperator.LogicalOr,

                _ => null
            };
        }
    }
}