using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class TypeBounds
{
    public static bool ValueFitsInType(long value, TypeNode targetType) => targetType switch
    {
        Int8TypeNode   => value is >= sbyte.MinValue and <= sbyte.MaxValue,
        Int16TypeNode  => value is >= short.MinValue and <= short.MaxValue,
        Int32TypeNode  => value is >= int.MinValue and <= int.MaxValue,
        Int64TypeNode  => true,
        UInt8TypeNode  => value is >= byte.MinValue and <= byte.MaxValue,
        UInt16TypeNode => value is >= ushort.MinValue and <= ushort.MaxValue,
        UInt32TypeNode => value is >= uint.MinValue and <= uint.MaxValue,
        UInt64TypeNode => value >= 0,
        _ => false
    };

    public static bool ValueFitsInFloat(double value, TypeNode targetType) => targetType switch
    {
        Float32TypeNode => value is >= float.MinValue and <= float.MaxValue,
        Float64TypeNode => true,
        _ => false
    };
}