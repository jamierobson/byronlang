using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class FunctionResolver
{
    private readonly List<FunctionDeclarationNode> _declarations;
    private readonly FunctionRegistry _functionRegistry;
    private readonly TypeRegistry _typeRegistry;
    private readonly Diagnostics _diagnostics;

    public FunctionResolver(FunctionRegistry functionRegistry, TypeRegistry typeRegistry, List<FunctionDeclarationNode> declarations, Diagnostics diagnostics)
    {
        _declarations = declarations;
        _functionRegistry = functionRegistry;
        _typeRegistry = typeRegistry;
        _diagnostics = diagnostics;
    }

    public void Resolve()
    {
        foreach (var declaration in _declarations)
        {
            if (!_typeRegistry.IsValidType(declaration.Signature.ReturnType))
            {
                _diagnostics.UndeclaredType(declaration.Signature.ReturnType, "return");
            }

            foreach (var param in declaration.Signature.Parameters)
            {
                if (!_typeRegistry.IsValidType(param.Type))
                {
                    _diagnostics.UndeclaredType(declaration.Signature.ReturnType, "parameter");
                }
            }

            if (!_functionRegistry.TryRegister(declaration))
            {
                _ = _functionRegistry.TryGetFunction(declaration.CanonicalName.ToString(), out var originalDeclaration);
                _diagnostics.Duplicate(declaration, originalDeclaration!.Declaration.Span);
            }
        }
    }
}