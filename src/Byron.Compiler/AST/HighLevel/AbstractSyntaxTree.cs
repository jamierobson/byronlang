using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST.HighLevel;

public class ProgramNode(List<TopLevelDeclarationNode> declarations)
{
    public readonly IReadOnlyList<TopLevelDeclarationNode> Declarations = declarations;
};

public readonly record struct NodeId(int Value) : IComparable<NodeId>
{
    public override string ToString() => $"#{Value}";
    public int CompareTo(NodeId other) => Value.CompareTo(other.Value);
}

public abstract class AstNode
{
    private static int _nextId;
    public NodeId Id { get; } = new(Interlocked.Increment(ref _nextId));
    public SourceSpan Span { get; init; }

    protected AstNode(SourceSpan span)
    {
        Span = span;
    }
}
