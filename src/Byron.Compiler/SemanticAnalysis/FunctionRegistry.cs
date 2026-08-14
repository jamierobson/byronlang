using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class FunctionRegistry
{
    private readonly Dictionary<string, FunctionSymbol> _declarations = [];
    public IReadOnlyDictionary<string, FunctionSymbol> Declarations => _declarations;
    
    public bool TryRegister(FunctionDeclarationNode declaration)
    {
        var canonicalName = declaration.CanonicalName.ToString();
        var symbol = new FunctionSymbol(
            declaration.CanonicalName,
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
        string[] modulePath, 
        string shortName, 
        [NotNullWhen(true)] out FunctionSymbol? function)
    {
        // todo: When this is 
        var canonicalNameString = CanonicalName.CanonicalNameString(modulePath, shortName);
        return _declarations.TryGetValue(canonicalNameString, out function) 
               || _declarations.TryGetValue(shortName, out function);
    }
}
