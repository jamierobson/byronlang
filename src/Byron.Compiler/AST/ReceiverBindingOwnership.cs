using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST;

public enum ReceiverBindingOwnership
{
    Owned,
    ImmutableBorrow,
    MutableBorrow,
    ImplicitCopy,
}