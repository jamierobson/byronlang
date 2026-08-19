using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST.HighLevel;

public abstract class TypeNode(Symbol symbol, SourceSpan span) : AstNode(span)
{
    public Symbol Symbol { get; init; } = symbol;

    protected TypeNode(string name, SourceSpan span) : this(Symbol.From(name), span)
    {
    }
}

public class NominalTypeNode(string name, SourceSpan span) : TypeNode(name, span)
{
    public NominalTypeNode(string[] segments, SourceSpan span) : this(string.Join('.', segments), span)
    {
    }
}

public class SelfTypeNode(TypeNode scopedType, SourceSpan span) : TypeNode(scopedType.Symbol, span)
{
    public TypeNode ScopedType { get; init; } = scopedType;
}

public class TraitTypeNode(string name, SourceSpan span) : TypeNode(name, span);

public class ReferenceTypeNode : TypeNode
{
    public TypeNode Target { get; init; }
    public bool IsMutable { get; init; }

    public ReferenceTypeNode(TypeNode target, bool isMutable, SourceSpan span) : base(target.Symbol, span) //todo: This might be a flaw. 
    {
        Target = target;
        IsMutable = isMutable;
    }
}

public abstract class BuiltInTypeNode(string name, SourceSpan span) : TypeNode(name, span);

public abstract class PrimitiveTypeNode(string name, SourceSpan span) : BuiltInTypeNode(name, span);

public class VoidTypeNode : PrimitiveTypeNode
{
    public VoidTypeNode(SourceSpan span) : base(PrimitiveTypeNames.@void, span) { }
}

public abstract class NumericTypeNode : PrimitiveTypeNode
{
    public bool Signed { get; init; }

    protected NumericTypeNode(string name, bool signed, SourceSpan span) : base(name, span)
    {
        Signed = signed;
    }
}

public abstract class IntegerTypeNode : NumericTypeNode
{
    public int BitWidth { get; init; }

    protected IntegerTypeNode(string name, int bitWidth, bool signed, SourceSpan span) : base(name, signed, span)
    {
        BitWidth = bitWidth;
    }
}

public class UnsignedIntTypeNode : IntegerTypeNode
{
    public UnsignedIntTypeNode(string name, int bitWidth, SourceSpan span) : base(name, bitWidth, false, span) { }
}

public class SignedIntTypeNode : IntegerTypeNode
{
    public SignedIntTypeNode(string name, int bitWidth, SourceSpan span) : base(name, bitWidth, true, span) { }
}

public class FloatTypeNode : NumericTypeNode
{
    public int BitWidth { get; init; }

    public FloatTypeNode(string name, int bitWidth, SourceSpan span) : base(name, true, span)
    {
        BitWidth = bitWidth;
    }
}

public class Int8TypeNode : SignedIntTypeNode
{
    public Int8TypeNode(SourceSpan span) : base(PrimitiveTypeNames.i8, 8, span) { }
}

public class Int16TypeNode : SignedIntTypeNode
{
    public Int16TypeNode(SourceSpan span) : base(PrimitiveTypeNames.i16, 16, span) { }
}

public class Int32TypeNode : SignedIntTypeNode
{
    public Int32TypeNode(SourceSpan span) : base(PrimitiveTypeNames.i32, 32, span) { }
}

public class Int64TypeNode : SignedIntTypeNode
{
    public Int64TypeNode(SourceSpan span) : base(PrimitiveTypeNames.i64, 64, span) { }
}

public class UInt8TypeNode : UnsignedIntTypeNode
{
    public UInt8TypeNode(SourceSpan span) : base(PrimitiveTypeNames.u8, 8, span) { }
}

public class UInt16TypeNode : UnsignedIntTypeNode
{
    public UInt16TypeNode(SourceSpan span) : base(PrimitiveTypeNames.u16, 16, span) { }
}

public class UInt32TypeTypeNode : UnsignedIntTypeNode
{
    public UInt32TypeTypeNode(SourceSpan span) : base(PrimitiveTypeNames.u32, 32, span) { }
}

public class UInt64TypeNode : UnsignedIntTypeNode
{
    public UInt64TypeNode(SourceSpan span) : base(PrimitiveTypeNames.u64, 64, span) { }
}

public class Float32TypeNode : FloatTypeNode
{
    public Float32TypeNode(SourceSpan span) : base(PrimitiveTypeNames.f32, 32, span) { }
}

public class Float64TypeNode : FloatTypeNode
{
    public Float64TypeNode(SourceSpan span) : base(PrimitiveTypeNames.f64, 64, span) { }
}

public class BoolTypeNode : PrimitiveTypeNode
{
    public BoolTypeNode(SourceSpan span) : base(PrimitiveTypeNames.boolean, span) { }
}

public class RuneTypeNode : PrimitiveTypeNode
{
    public RuneTypeNode(SourceSpan span) : base(PrimitiveTypeNames.rune, span) { }
}