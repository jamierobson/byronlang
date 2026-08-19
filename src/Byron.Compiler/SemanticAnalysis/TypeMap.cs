using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class TypeMap
{
    private readonly Dictionary<NodeId, TypeNode> _nodeTypes = new();

    public void SetType(ExpressionNode node, TypeNode type)
    {
        _nodeTypes[node.Id] = type;
    }

    public TypeNode GetType(ExpressionNode node)
    {
        if (_nodeTypes.TryGetValue(node.Id, out var type))
        {
            return type;
        }

        throw new InvalidOperationException($"Node {node.GetType().Name} (Id: {node}) has not been assigned a type at {node.Span}.");
    }

    public bool TryGetType(ExpressionNode node, [NotNullWhen(true)] out TypeNode? type)
    {
        return _nodeTypes.TryGetValue(node.Id, out type);
    }
}