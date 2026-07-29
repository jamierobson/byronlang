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
        UInt8TypeNode => new UnsignedInt(8),
        UInt16TypeNode => new UnsignedInt(16),
        UInt32TypeNode => new UnsignedInt(32),
        UInt64TypeNode => new UnsignedInt(64),
        Int8TypeNode => new Int(8),
        Int16TypeNode => new Int(16),
        Int32TypeNode or RuneTypeNode => new Int(32),
        Int64TypeNode => new Int(64),
        Float32TypeNode => new Float(32),
        Float64TypeNode => new Float(64),
        BoolTypeNode => new Boolean(),
        VoidTypeNode or UnitTypeNode => new Void(),
        UserDeclaredTypeNode user => new Struct(user.FullyQualifiedName),
        ReferenceTypeNode refType => new Pointer(From(refType.Target)),
        _ => throw new ByronNotImplementedException(node.GetType(), typeof(LlvmType))
    };
}