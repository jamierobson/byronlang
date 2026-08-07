using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Lexer;

namespace Byron.Compiler.SemanticAnalysis;

public enum ResolutionState
{
    Unresolved,
    Resolving,
    Resolved
}

public class TypeResolver
{
    private readonly TypeRegistry _typeRegistry;
    private readonly Dictionary<string, StructDeclarationNode> _declarations = new();
    private readonly Dictionary<string, ResolutionState> _resolutionStates = new();
    private readonly Diagnostics _diagnostics;

    public TypeResolver(
        TypeRegistry typeRegistry, 
        IEnumerable<StructDeclarationNode> structDeclarations, 
        Diagnostics diagnostics)
    {
        _typeRegistry = typeRegistry;
        _diagnostics = diagnostics;

        foreach (var structDeclarationNode in structDeclarations)
        {
            var canonicalName = structDeclarationNode.CanonicalName();
            _declarations.Add(canonicalName, structDeclarationNode);
            _resolutionStates[canonicalName] = ResolutionState.Unresolved;
        }
    }

    public void Resolve()
    {
        foreach (var declaration in _declarations)
        {
            _ = EnsureResolved(declaration);
        }
    }

    private bool EnsureResolved(KeyValuePair<string, StructDeclarationNode> declaration)
    {
        if (_resolutionStates.TryGetValue(declaration.Key, out var state) && state == ResolutionState.Resolved)
        {
            return true;
        }
        
        foreach (var field in declaration.Value.Fields)
        {
            if (!EnsureResolved(field.Type))
            {
                return false;
            }
        }

        if (_typeRegistry.IsValidStructName(declaration.Value.Name) && _typeRegistry.IsValidStructName(declaration.Key))
        {
            _resolutionStates[declaration.Key] = ResolutionState.Resolved;
            if (_typeRegistry.TryRegister(declaration.Value))
            {
                return true;
            }
            
            _ = _typeRegistry.TryGetStruct(declaration.Value.Name, out var duplicateDeclaration);
            
            _diagnostics.Duplicate(declaration.Value, duplicateDeclaration!.Span);
            return false;
        }
        _diagnostics.InvalidStructName(declaration.Value, declaration.Key);
        return false;
    }

    public bool EnsureResolved(TypeNode typeNode) => EnsureResolved(typeNode, typeNode.Span);
    
    public bool EnsureResolved(TypeNode typeNode, SourceSpan sourceSpan)
    {
        if (_typeRegistry.IsValidType(typeNode))
        {
            return true;
        }

        if (typeNode is ReferenceTypeNode referenceTypeNode)
        {
            return EnsureResolved(referenceTypeNode.Target, sourceSpan);
        }

        var canonicalName =  typeNode.CanonicalName();
        if (!_declarations.TryGetValue(canonicalName, out var structDeclaration))
        {
            
            _diagnostics.UndeclaredType(typeNode);
            return false;
        }

        var state = _resolutionStates.GetValueOrDefault(canonicalName, ResolutionState.Unresolved);

        if (state == ResolutionState.Resolved)
        {
            return true;
        }

        if (state == ResolutionState.Resolving)
        {
            _diagnostics.CircularReference(canonicalName, sourceSpan);
            return false;
        }

        _resolutionStates[canonicalName] = ResolutionState.Resolving;

        var hasErrors = false;
        foreach (var field in structDeclaration.Fields)
        {
            if (!EnsureResolved(field.Type))
            {
                hasErrors = true;
            }
        }

        if (hasErrors)
        {
            return false;
        }

        if (_typeRegistry.IsValidStructName(structDeclaration.Name) && _typeRegistry.IsValidStructName(canonicalName))
        {
            _resolutionStates[canonicalName] = ResolutionState.Resolved;

            if (_typeRegistry.TryRegister(structDeclaration))
            {
                return true;
            }
            
            _ = _typeRegistry.TryGetStruct(canonicalName, out var duplicateDeclaration);
            
            _diagnostics.Duplicate(structDeclaration, duplicateDeclaration!.Span);
            return false;

        }
        
        _diagnostics.InvalidStructName(structDeclaration, canonicalName);
        return false;
    }
}