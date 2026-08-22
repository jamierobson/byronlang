using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST.HighLevel;

public class ProgramNode(List<FileModuleNode> modules)
{
    public readonly IReadOnlyList<FileModuleNode> RootModules = modules;
};

public abstract class ModuleDeclarationNode(string name, SourceSpan span) : TopLevelDeclarationNode(name, span)
{
    public ModuleDeclarationCollection Declarations { get; } = new();
}

public class FileModuleNode(string fileName, SourceSpan span) : ModuleDeclarationNode(fileName, span);

public class BlockModuleNode(string name, SourceSpan span) : ModuleDeclarationNode(name, span);

public class ModuleDeclarationCollection
{
    public List<FunctionDeclarationNode> Functions { get; } = new();
    public List<StructDeclarationNode> Structs { get; } = new();
    public List<TraitDeclarationNode> Traits { get; } = new();
    public List<BlockModuleNode> ChildModules { get; } = new();
    public List<ImplementBlockDeclarationNode> ImplementBlocks { get; } = new();
}

public readonly record struct NodeId(int Value) : IComparable<NodeId>
{
    public override string ToString() => $"#{Value}";
    public int CompareTo(NodeId other) => Value.CompareTo(other.Value);
}

public abstract class AstNode(SourceSpan span)
{
    private static int _nextId;
    public NodeId Id { get; } = new(Interlocked.Increment(ref _nextId));
    public SourceSpan Span { get; init; } = span;
}
