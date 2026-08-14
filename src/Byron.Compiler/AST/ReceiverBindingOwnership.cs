using Byron.Compiler.SemanticAnalysis;

namespace Byron.Compiler.AST;

public enum ReceiverBindingOwnership
{
    Owned,
    ImmutableBorrow,
    MutableBorrow,
    ImplicitCopy,
}

public static class ReceiverBindingOwnershipExtensions
{
    extension(ReceiverBindingOwnership ownership)
    {
        public bool IsMutable() => ownership is ReceiverBindingOwnership.MutableBorrow or ReceiverBindingOwnership.Owned;
        public bool IsBorrow() => ownership is ReceiverBindingOwnership.MutableBorrow or ReceiverBindingOwnership.ImmutableBorrow;
        public bool IsReference() => ownership is ReceiverBindingOwnership.MutableBorrow or ReceiverBindingOwnership.ImmutableBorrow or ReceiverBindingOwnership.Owned;
    }
}