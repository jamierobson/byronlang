using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class TypeBounds
{
    public static bool CanCoerceToType(long value, NumericTypeNode targetType) => targetType switch
    {
        Int8TypeNode   => value is >= sbyte.MinValue and <= sbyte.MaxValue,
        Int16TypeNode  => value is >= short.MinValue and <= short.MaxValue,
        Int32TypeNode  => value is >= int.MinValue and <= int.MaxValue,
        Int64TypeNode  => true,
        UInt8TypeNode  => value is >= byte.MinValue and <= byte.MaxValue,
        UInt16TypeNode => value is >= ushort.MinValue and <= ushort.MaxValue,
        UInt32TypeTypeNode => value is >= uint.MinValue and <= uint.MaxValue,
        UInt64TypeNode => value >= 0,
        Float32TypeNode => true,
        Float64TypeNode => true,
        _ => false
    };

    public static bool CanCoerceToType(double value, NumericTypeNode targetType) => targetType switch
    {
        Float32TypeNode => value is >= float.MinValue and <= float.MaxValue,
        Float64TypeNode => true,
        SignedIntTypeNode or UnsignedIntTypeNode when Math.Abs(value % 1.0) < double.Epsilon => value is >= long.MinValue and <= long.MaxValue && CanCoerceToType((long)value, targetType),
        _ => false
    };

    public static bool CanCoerceToType(ExpressionNode expression, NumericTypeNode targetType)
    {
        if (expression is IntegerLiteralNode integerLiteral)
        {
            return CanCoerceToType(integerLiteral.Value, targetType);
        }
        if (expression is FloatLiteralNode floatLiteral)
        {
            return CanCoerceToType(floatLiteral.Value, targetType);
        }
        return false;
    }
}