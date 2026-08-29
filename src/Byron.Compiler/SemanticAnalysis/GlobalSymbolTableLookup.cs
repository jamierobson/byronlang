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
            : TryResolveCanonicalType(module, typeNode, out _);
    }
    
    public bool TryResolveCanonicalType(
        ModuleDeclarationNode activeModule,
        TypeNode inputType,
        [NotNullWhen(true)] out TypeNode? resolvedType)
    {
        var leafName = inputType.Symbol.MemberName;
        if (inputType.Symbol.Segments.Length <= 1 && symbols.Primitives.ContainsKey(leafName))
        {
            resolvedType = inputType;
            return true;
        }

        var success = TryResolveSymbol(
            activeModule,
            inputType.Symbol, 
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
        [NotNullWhen(true)] out StructDeclarationNode? declaration)
    {
        var success = TryResolveSymbol(
            activeModule,
            Symbol.From(name), 
            symbols.NominalTypes.CandidateSymbolsForMemberNamedElement,
            symbols.Structs,
            out var resolved);

        declaration = resolved;
        return success;
    }
    
    public bool TryGetTrait(
        ModuleDeclarationNode activeModule,
        Symbol symbol, 
        [NotNullWhen(true)] out TraitDeclarationNode? resolvedTrait)
    {
        var success = TryResolveSymbol(
            activeModule,
            symbol, 
            symbols.Traits.CandidateSymbolsForMemberNamedElement,
            symbols.Traits.Symbols,
            out var resolved);
        
        resolvedTrait = resolved;
        return success;
    }

    public bool TryGetFunction(
        ModuleDeclarationNode activeModule,
        Symbol symbol,
        [NotNullWhen(true)] out FunctionDeclarationNode? function)
    {
        if (TryResolveSymbol(
                activeModule,
                symbol,
                symbols.Functions.CandidateSymbolsForMemberNamedElement,
                symbols.Functions.Symbols,
                out function))
        {
            return true;
        }

        if (TryResolveTraitFunction(
                activeModule,
                symbol,
                out function))
        {
            return true;
        }

        return false;
    }
    
    private bool TryResolveTraitFunction(
        ModuleDeclarationNode module,
        Symbol rawSymbol,
        [NotNullWhen(true)] out FunctionDeclarationNode? function)
    {

        if (!symbols.Functions.CandidateSymbolsForMemberNamedElement.TryGetValue(rawSymbol.MemberName,
                out var functionCandidates))
        {
            function = null;
            return false;
        }

        function = null;

        if (rawSymbol.Segments.Length < 3)
        {
             // The trait-function symbol is Type.Trait.fn.
             // The shortest possible trait alias is AliasedType.AliasedTrait.function
            function = null;
            return false;
        }

        var functionName = rawSymbol.MemberName;
        var path = rawSymbol.Path;

        for (var split = path.Length - 1; split >= 1; split--)
        {
            var typeSegments = path[..split];
            var traitSegments = path[split..];

            if (!TryResolveSymbol(
                    module, Symbol.From(traitSegments),
                    symbols.Traits.CandidateSymbolsForMemberNamedElement,
                    symbols.Traits.Symbols,
                    out var traitNode))
            {
                continue;
            }

            if (!TryResolveSymbol(
                    module, Symbol.From(typeSegments),
                    symbols.NominalTypes.CandidateSymbolsForMemberNamedElement,
                    symbols.NominalTypes.Symbols,
                    out var typeNode))
            {
                continue;
            }

            var candidateFunctionSymbol = Symbol.From([
                ..typeNode!.Symbol.Segments,
                ..traitNode!.Symbol.Segments,
                functionName
            ]);

            if (symbols.Functions.TryGet(candidateFunctionSymbol, out function))
            {
                return true;
            }
        }

        function = null;
        return false;
    }
    
    private bool TryGetTypeCandidateSymbol(ModuleDeclarationNode module, Symbol functionCandidateSymbol,
        Symbol traitCandidateSymbol, [NotNullWhen(true)] out TypeNode? candidateTypeNode)
    {
        var functionTypePath = functionCandidateSymbol.Path;
        
        // The function candidate symbol is already validated longer than the trait candidate symbol
        for (var i = traitCandidateSymbol.Segments.Length; i > 0; i--)
        {
            if (traitCandidateSymbol.Segments[^i] != functionTypePath[^i])
            {
                candidateTypeNode = null;
                return false;
            }
        }

        var candidateTypeSymbol = Symbol.From(functionTypePath[0..^traitCandidateSymbol.Segments.Length]);
        candidateTypeNode = new LookupTypeNode(candidateTypeSymbol); //todo: This is of course just to get compilation oing
        return true;
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
        
        if (candidatesByMemberName.TryGetValue(candidateSymbol.MemberName, out var candidates))
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

    public ModuleDeclarationNode GetEncapsulatingModule(TraitDeclarationNode resolvedTrait) => symbols.Traits.EncapsulatingModules[resolvedTrait.Symbol];
}