using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class GlobalSymbolTableLookup(GlobalSymbolTable symbols)
{    public bool TryResolveCanonicalType(
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

        if (symbols.NominalTypes.TryGet(inputType.Symbol, out var exactNominal))
        {
            resolvedType = exactNominal;
            return true;
        }

        if (!symbols.NominalTypes.CandidateSymbolsForMemberNamedElement.TryGetValue(leafName, out var candidates))
        {
            resolvedType = null;
            return false;
        }
        
        for (var i = currentScopeSegments.Length; i >= 0; i--)
        {
            var prefix = currentScopeSegments[..i];
            var candidateSegments = GlobalSymbolTable.CreateCanonicalSymbolSegments(prefix, inputType.Symbol.Segments);
            var candidateSymbol = new Symbol(candidateSegments);

            if (candidates.Contains(candidateSymbol) && symbols.NominalTypes.TryGet(candidateSymbol, out var resolvedNominalType))
            {
                resolvedType = resolvedNominalType;
                return true;
            }
        }

        var rootQuerySegment = inputType.Symbol.Segments[0];

        if (TryResolveImportAlias(currentModule, rootQuerySegment, out var expandedPrefix))
        {
            var combinedSegments = GlobalSymbolTable.CreateCanonicalSymbolSegments(expandedPrefix, inputType.Symbol.Segments[1..]);
            var aliasedSymbol = new Symbol(combinedSegments);

            if (candidates.Contains(aliasedSymbol) && symbols.NominalTypes.TryGet(aliasedSymbol, out var resolvedNominalType))
            {
                resolvedType = resolvedNominalType;
                return true;
            }
        }

        resolvedType = null;
        return false;
    }

    private static bool TryResolveImportAlias(ModuleDeclarationNode module, string alias, out string[] expandedPrefix)
    {
        expandedPrefix = [];
        return false;
    }
    
    public bool TypeExists(TypeNode typeNode)
    {
        return typeNode is PrimitiveTypeNode p
            ? symbols.Primitives.ContainsKey(p.Symbol.ToString())
            : symbols.NominalTypes.TryGet(typeNode.Symbol, out _);
    }

    public IReadOnlyDictionary<Symbol, StructDeclarationNode> Structs => symbols.Structs;

    public bool TryGetStruct(string name, string[] symbolSegments, [NotNullWhen(true)] out StructDeclarationNode? declaration)
    {
    }

    public bool TryGetStruct(TypeNode type, string[] symbolSegments, [NotNullWhen(true)] out StructDeclarationNode? declaration)
    {
        if (type is not NominalTypeNode nominalTypeNode)
        {
            declaration = null;
            return false;
        }
    }

    public bool TryGetFunction(string[] namespaceSegments, string functionName, [NotNullWhen(true)] out FunctionDeclarationNode? function)
    {
    }
}