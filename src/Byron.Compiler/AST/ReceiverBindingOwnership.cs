namespace Byron.Compiler.AST;

public enum ReceiverBindingOwnership
{
    Owned,
    ImmutableBorrow,
    MutableBorrow,
    ImplicitCopy,
}