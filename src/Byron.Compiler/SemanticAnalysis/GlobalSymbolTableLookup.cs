using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class GlobalSymbolTableLookup(GlobalSymbolTable symbols)
{    
    public bool TryResolveCanonicalType(
        TypeNode inputType,
        string[] currentScopeSegments,
        ModuleDeclarationNode currentModule,
        [NotNullWhen(true)] out TypeNode? resolvedType)
    {
        var leafName = inputType.Symbol.MemberName;
        if (inputType.Symbol.Segments.Length <= 1 && symbols.Primitives.ContainsKey(leafName))
        {
            resolvedType = inputType;
            return true;
        }

        var success = TryResolveSymbol(
            inputType.Symbol.Segments, currentScopeSegments, currentModule,
            symbols.NominalTypes.CandidateSymbolsForMemberNamedElement,
            symbols.NominalTypes.Symbols,
            out var resolvedNominal);
        
        resolvedType = resolvedNominal;
        return success;
    }

    private static bool TryResolveImportAlias(ModuleDeclarationNode module, string alias, out string[] expandedPrefix)
    {
        expandedPrefix = [];
        return false;
    }
    
    public bool TypeExists(ModuleDeclarationNode module, TypeNode typeNode)
    {
        return typeNode is PrimitiveTypeNode p
            ? symbols.Primitives.ContainsKey(p.Symbol.ToString())
            : TryResolveCanonicalType(typeNode, module.Symbol.Segments, module, out _);
    }

    public IReadOnlyDictionary<Symbol, StructDeclarationNode> Structs => symbols.Structs;

    public bool TryGetStruct(ModuleDeclarationNode module, string name, string[] symbolSegments, [NotNullWhen(true)] out StructDeclarationNode? declaration) => TryResolveSymbol(
        [name], 
        symbolSegments, 
        module,
        symbols.NominalTypes.CandidateSymbolsForMemberNamedElement,
        symbols.Structs,
        out declaration);

    public bool TryGetStruct(TypeNode type, [NotNullWhen(true)] out StructDeclarationNode? declaration)
    {
        if (type is not NominalTypeNode nominalTypeNode)
        {
            declaration = null;
            return false;
        }

        declaration = symbols.Structs[nominalTypeNode.Symbol];
        return true;
    }

    public bool TryGetFunction(
        ModuleDeclarationNode currentModule,
        string[] namespaceSegments,
        string functionName,
        string[] currentScopeSegments,
        [NotNullWhen(true)] out FunctionDeclarationNode? function)
    {
        string[] querySegments = [.. namespaceSegments, functionName];
        return TryResolveSymbol(
            querySegments, currentScopeSegments, currentModule,
            symbols.Functions.CandidateSymbolsForMemberNamedElement,
            symbols.Functions.Symbols,
            out function);
    }
    
    private bool TryResolveSymbol<T>(
        string[] querySegments,
        string[] currentScopeSegments,
        ModuleDeclarationNode currentModule,
        IReadOnlyDictionary<string, HashSet<Symbol>> candidatesByMemberName,
        IReadOnlyDictionary<Symbol, T> symbolTable,
        [NotNullWhen(true)] out T? result)
        where T : class
    {
        var exactSymbol = new Symbol(querySegments);
        if (symbolTable.TryGetValue(exactSymbol, out result))
        {
            return true;
        }

        var leafName = querySegments[^1];
        if (!candidatesByMemberName.TryGetValue(leafName, out var candidates))
        {
            result = null;
            return false;
        }

        for (var i = currentScopeSegments.Length; i >= 0; i--)
        {
            var prefix = currentScopeSegments[..i];
            var candidateSegments = GlobalSymbolTable.CreateCanonicalSymbolSegments(prefix, querySegments);
            var candidateSymbol = new Symbol(candidateSegments);

            if (candidates.Contains(candidateSymbol) && symbolTable.TryGetValue(candidateSymbol, out result))
            {
                
                return true;
            }
        }

        if (TryResolveImportAlias(currentModule, querySegments[0], out var expandedPrefix))
        {
            var combinedSegments = GlobalSymbolTable.CreateCanonicalSymbolSegments(expandedPrefix, querySegments[1..]);
            var aliasedSymbol = new Symbol(combinedSegments);

            if (candidates.Contains(aliasedSymbol) && symbolTable.TryGetValue(aliasedSymbol, out result))
            {
                return true;
            }
        }

        result = null;
        return false;
    }
}