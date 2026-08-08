using Byron.Compiler.AST.LowLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.CodeGen;

public abstract record LlvmType
{
    public sealed override string ToString() => ToIrString();

    protected abstract string ToIrString();

    public record Boolean() : Int(1);
    public record UnsignedInt(int BitWidth) : Int(BitWidth);
    
    public record Int(int BitWidth) : LlvmType 
    { 
        protected override string ToIrString() => $"i{BitWidth}"; 
    }
    
    public record Float(int BitWidth) : LlvmType 
    { 
        protected override string ToIrString() => BitWidth == 32 ? "float" : "double"; 
    }
    
    public record Void : LlvmType 
    { 
        protected override string ToIrString() => "void"; 
    }

    public record Pointer(LlvmType ElementType) : LlvmType
    {
        protected override string ToIrString() => $"{ElementType.ToIrString()}*";
    }

    public record Struct(string Name) : LlvmType
    {
        protected override string ToIrString() => $"%{Name.Replace('.', '_')}";
    }

    public static LlvmType From(TypeNode node) => node switch
    {
        UnsignedIntTypeNode @uint => new UnsignedInt(@uint.BitWidth),
        SignedIntTypeNode @int => new Int(@int.BitWidth),
        FloatTypeNode @float => new Int(@float.BitWidth),
        BoolTypeNode => new Boolean(),
        VoidTypeNode => new Void(),
        NominalTypeNode user => new Struct(user.CanonicalName),
        ReferenceTypeNode refType => new Pointer(From(refType.Target)),
        _ => throw new ByronNotImplementedException(node.GetType(), typeof(LlvmType))
    };
}