using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public record ParameterSymbol(string Name, TypeNode Type);

public record FunctionSymbol(
    string CanonicalName,
    List<string> ModulePath,
    string Name,
    List<ParameterSymbol> Parameters,
    TypeNode ReturnType,
    FunctionDeclarationNode Declaration
);

public class FunctionRegistry
{
    private readonly Dictionary<string, FunctionSymbol> _declarations = [];
    public IReadOnlyDictionary<string, FunctionSymbol> Declarations => _declarations;
    
    public bool TryRegister(FunctionDeclarationNode declaration)
    {
        var canonicalName = declaration.CanonicalName();
        var symbol = new FunctionSymbol(
            canonicalName,
            declaration.ModulePath,
            declaration.Name,
            declaration.Parameters.Select(p => new ParameterSymbol(p.Name, p.Type)).ToList(),
            declaration.ReturnType,
            declaration
        );

        return _declarations.TryAdd(canonicalName, symbol);
    }
    
    public bool TryGetFunction(string canonicalName, [NotNullWhen(true)] out FunctionSymbol? function)
    {
        return _declarations.TryGetValue(canonicalName, out function);
    }
    
    public bool TryGetFunctionInScope(
        List<string> modulePath, 
        string shortName, 
        [NotNullWhen(true)] out FunctionSymbol? function)
    {
        var canonicalName = CanonicalNames.InModule(modulePath, shortName);
        return _declarations.TryGetValue(canonicalName, out function) 
               || _declarations.TryGetValue(shortName, out function);
    }
}
