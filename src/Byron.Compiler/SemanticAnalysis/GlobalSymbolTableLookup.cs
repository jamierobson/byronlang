using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class GlobalSymbolTableLookup(GlobalSymbolTable symbols)
{    
    public IReadOnlyDictionary<Symbol, StructDeclarationNode> Structs => symbols.Structs;
    public bool TypeExists(ModuleDeclarationNode module, TypeNode typeNode)
    {
        return typeNode is PrimitiveTypeNode p
            ? symbols.Primitives.ContainsKey(p.Symbol.ToString())
            : TryResolveCanonicalType(module, typeNode, module.Symbol.Segments, out _);
    }
    
    public bool TryResolveCanonicalType(
        ModuleDeclarationNode activeModule,
        TypeNode inputType,
        string[] currentScopeSegments,
        [NotNullWhen(true)] out TypeNode? resolvedType)
    {
        _ = currentScopeSegments; // Ignored and will be removed
        var leafName = inputType.Symbol.MemberName;
        if (inputType.Symbol.Segments.Length <= 1 && symbols.Primitives.ContainsKey(leafName))
        {
            resolvedType = inputType;
            return true;
        }

        var success = TryResolveSymbol(
            activeModule,
            inputType.Symbol, 
            // currentScopeSegments, 
            symbols.NominalTypes.CandidateSymbolsForMemberNamedElement,
            symbols.NominalTypes.Symbols,
            out var resolvedNominal);
        
        resolvedType = resolvedNominal;
        return success;
    }

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

    public bool TryGetStruct(
        ModuleDeclarationNode activeModule,
        string name,
        string[] symbolSegments,
        [NotNullWhen(true)] out StructDeclarationNode? declaration)
    {
        _ = symbolSegments;
        var success = TryResolveSymbol(
            activeModule,
            Symbol.From(name), 
            // symbolSegments, 
            symbols.NominalTypes.CandidateSymbolsForMemberNamedElement,
            symbols.Structs,
            out var resolved);

        declaration = resolved;
        return success;
    }
    
    public bool TryGetTrait(
        ModuleDeclarationNode activeModule,
        Symbol symbol, 
        string[] currentScopeSegments,
        [NotNullWhen(true)] out TraitDeclarationNode? resolvedTrait)
    {
        _ = currentScopeSegments; // Ignored and will be removed
        var success = TryResolveSymbol(
            activeModule,
            symbol, 
            // currentScopeSegments, 
            symbols.Traits.CandidateSymbolsForMemberNamedElement,
            symbols.Traits.Symbols,
            out var resolved);
        
        resolvedTrait = resolved;
        return success;
    }

    public bool TryGetFunction(
        ModuleDeclarationNode activeModule,
        Symbol symbol,
        // string[] namespaceSegments,
        string functionName,
        string[] currentScopeSegments,
        [NotNullWhen(true)] out FunctionDeclarationNode? function)
    {
        // Ignored deliberately
        _ = functionName;
        _ = currentScopeSegments;
        return TryResolveSymbol(
            activeModule,
            symbol, 
            // currentScopeSegments, 
            symbols.Functions.CandidateSymbolsForMemberNamedElement,
            symbols.Functions.Symbols,
            out function);
    }
    
    
    private bool TryResolveSymbol<T>(
        ModuleDeclarationNode module,
        Symbol rawSymbol,
        IReadOnlyDictionary<string, HashSet<Symbol>> candidatesByMemberName,
        IReadOnlyDictionary<Symbol, T> symbolTable,
        [MaybeNullWhen(false)] out T? result
    )
    {
        if (TryResolveCandidateSymbol(module, rawSymbol, candidatesByMemberName, symbolTable, out result))
        {
            return true;
        }

        if (symbols.ModuleAliases.TryGetValue(module.Symbol, out var aliases))
        {
            var candidateSymbol = ReplaceAliases(rawSymbol, aliases);
            
            if (candidateSymbol != rawSymbol && TryResolveCandidateSymbol(module, candidateSymbol, candidatesByMemberName, symbolTable, out result))
            {
                return true;
            }
        }
        
        result = default;
        return false;
    }

    private bool TryResolveCandidateSymbol<T>(
        ModuleDeclarationNode module,
        Symbol candidateSymbol,
        IReadOnlyDictionary<string, HashSet<Symbol>> candidatesByMemberName,
        IReadOnlyDictionary<Symbol, T> symbolTable,
        [MaybeNullWhen(false)] out T? result)
    { 
        if (symbolTable.TryGetValue(candidateSymbol, out result))
        {
            return true;
        }
        
        if (candidatesByMemberName.TryGetValue(candidateSymbol.Segments[^1], out var candidates))
        {
            var scope = module.Symbol.Segments;
            for (var i = scope.Length; i >= 0; i--)
            {
                var scopedPrefix = scope[..i];
                var qualifiedSegments = GlobalSymbolTable.CreateCanonicalSymbolSegments(scopedPrefix, candidateSymbol.Segments);
                var candidate = Symbol.From(qualifiedSegments);

                if (candidates.Contains(candidate) && symbolTable.TryGetValue(candidate, out result))
                {
                    return true;
                }
            }
            
            
            // var moduleQualifiedCandidate = GlobalSymbolTable.CreateCanonicalSymbolSegments(module.Symbol.Segments, candidateSymbol.Segments);
            // candidateSymbol = Symbol.From(moduleQualifiedCandidate);
            // if (candidates.Contains(candidateSymbol) && symbolTable.TryGetValue(candidateSymbol, out result))
            // {
            //     return true;
            // }
        }

        return false;
    }

    private Symbol ReplaceAliases(Symbol candidateSymbol, Dictionary<string, Symbol> aliases)
    {
        var unaliasedSymbolPath = new List<string>();
        
        var head = candidateSymbol.Segments[0];
        if (aliases.TryGetValue(head, out var alias))
        {
            unaliasedSymbolPath.AddRange(alias.Segments);
            for (var i = 1; i < candidateSymbol.Segments.Length; i++)
            {
                unaliasedSymbolPath.Add(candidateSymbol.Segments[i]);
            }
            
            return Symbol.From(unaliasedSymbolPath);
        }
        
        return candidateSymbol;
    }
}