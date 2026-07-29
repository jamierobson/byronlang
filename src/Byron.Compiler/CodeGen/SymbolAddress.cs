namespace Byron.Compiler.CodeGen;

public class SymbolAddress
{
    public Value Pointer { get; }
    public LlvmType LlvmType { get; }
    public SymbolAddress(Value pointer, LlvmType llvmType)
    {
        Pointer = pointer;
        LlvmType = llvmType;
    }
}

public abstract record Value
{
    public sealed override string ToString() => ToIrString();
    protected abstract string ToIrString();

    public record Register(string Name) : Value
    {
        protected override string ToIrString() => $"%{Name}";
    }

    public record ConstantInt(long Value, int BitWidth = 32) : Value
    {
        protected override string ToIrString() => Value.ToString();
    }

    public record ZeroInitializer : Value
    {
        protected override string ToIrString() => "zeroinitializer";
    }
}