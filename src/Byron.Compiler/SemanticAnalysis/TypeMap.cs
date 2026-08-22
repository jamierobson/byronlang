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

public class CanonicalResolvingTypeMap(TypeMap typeMap, GlobalSymbolTableLookup globalSymbolTableLookup, Diagnostics diagnostics)
{    
    public bool TryGetType(ExpressionNode node, [NotNullWhen(true)] out TypeNode? type) => typeMap.TryGetType(node, out type);
    public TypeNode GetType(ExpressionNode node) => typeMap.GetType(node);
    public void SetType(ExpressionNode expression, TypeNode type) => typeMap.SetType(expression, type);
    public void SetType(ModuleDeclarationNode module, ExpressionNode expression, TypeNode possiblyUnresolvedType)
    {
        if (!globalSymbolTableLookup.TryResolveCanonicalType(possiblyUnresolvedType, module.Symbol.Segments, module, out var resolvedType))
        {
            diagnostics.UndeclaredType(possiblyUnresolvedType);
            return;
        }

        if (possiblyUnresolvedType is ReferenceTypeNode reference)
        {
            var canonicalReference = new ReferenceTypeNode(resolvedType, reference.IsMutable, reference.Span);
            typeMap.SetType(expression, canonicalReference);
        }
        else
        {
            typeMap.SetType(expression, resolvedType);
        }
    }
}