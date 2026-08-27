using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class GlobalSymbolTable
{
    private bool _entryFunctionEncountered = false;
    
    public readonly IReadOnlyDictionary<string, PrimitiveTypeSymbol> Primitives =
        new Dictionary<string, PrimitiveTypeSymbol>
        {
            { PrimitiveTypeNames.i8, new PrimitiveTypeSymbol(PrimitiveTypeNames.i8, 1, true) },
            { PrimitiveTypeNames.i16, new PrimitiveTypeSymbol(PrimitiveTypeNames.i16, 2, true) },
            { PrimitiveTypeNames.i32, new PrimitiveTypeSymbol(PrimitiveTypeNames.i32, 4, true) },
            { PrimitiveTypeNames.i64, new PrimitiveTypeSymbol(PrimitiveTypeNames.i64, 8, true) },

            { PrimitiveTypeNames.u8, new PrimitiveTypeSymbol(PrimitiveTypeNames.u8, 1, false) },
            { PrimitiveTypeNames.u16, new PrimitiveTypeSymbol(PrimitiveTypeNames.u16, 2, false) },
            { PrimitiveTypeNames.u32, new PrimitiveTypeSymbol(PrimitiveTypeNames.u32, 4, false) },
            { PrimitiveTypeNames.u64, new PrimitiveTypeSymbol(PrimitiveTypeNames.u64, 8, false) },

            { PrimitiveTypeNames.f32, new PrimitiveTypeSymbol(PrimitiveTypeNames.f32, 4, true) },
            { PrimitiveTypeNames.f64, new PrimitiveTypeSymbol(PrimitiveTypeNames.f64, 8, true) },

            { PrimitiveTypeNames.boolean, new PrimitiveTypeSymbol(PrimitiveTypeNames.boolean, 1, false) },
            { PrimitiveTypeNames.rune, new PrimitiveTypeSymbol(PrimitiveTypeNames.rune, 4, false) },
            { PrimitiveTypeNames.@void, new PrimitiveTypeSymbol(PrimitiveTypeNames.@void, 1, false) },
        };
    
    private readonly Dictionary<Symbol, StructDeclarationNode> _structs = new();
    public IReadOnlyDictionary<Symbol, StructDeclarationNode> Structs => _structs;
    
    public readonly SymbolList<FunctionDeclarationNode> Functions = new();
    public readonly SymbolList<TraitDeclarationNode> Traits = new();
    public readonly SymbolList<NominalTypeNode> NominalTypes = new();
    public readonly SymbolList<ModuleDeclarationNode> Modules = new();
    
    private Dictionary<Symbol, Dictionary<string, Symbol>> _moduleAliases = new();
    public IReadOnlyDictionary<Symbol, Dictionary<string, Symbol>> ModuleAliases => _moduleAliases;
    private readonly Dictionary<Symbol, Dictionary<string, AliasDeclarationNode>> _localAliases = new(); 
    
    

    public void Register(IReadOnlyList<ModuleDeclarationNode> programRootModules, Diagnostics diagnostics)
    {        
        foreach (var fileModule in programRootModules)
        {
            RegisterModules(fileModule, diagnostics);
        }
        
        foreach (var fileModule in programRootModules)
        {
            RegisterAliasSymbols(fileModule, diagnostics);
        }

        foreach (var fileModule in programRootModules)
        {
            BuildAliasContexts(fileModule, [], diagnostics);
        }
        
        foreach (var fileModule in programRootModules)
        {
            RegisterTypeSymbols(fileModule, [], diagnostics);
        }

        foreach (var fileModule in programRootModules)
        {
            RegisterFunctionSymbols(fileModule, [], diagnostics);
        }
    }
    
    public void RegisterFunctionSymbols(ModuleDeclarationNode module, string[] parentNamespaceSegments, Diagnostics diagnostics)
    {
        var thisNamespaceSegments = CreateCanonicalSymbolSegments(parentNamespaceSegments, module.Symbol.Segments);
        foreach (var function in module.Declarations.Functions)
        {
            if (function.Symbol.MemberName == FunctionSignatureNode.EntryFunctionName)
            {
                if (_entryFunctionEncountered)
                {
                    diagnostics.AmbiguousEntryPoint(function, function.Span);
                }
                else
                {
                    _entryFunctionEncountered = true;
                    RegisterFunction(function, [], diagnostics);                    
                }
            }
            else
            {
                RegisterFunction(function, thisNamespaceSegments, diagnostics);
            }
        }

        var lookup = new GlobalSymbolTableLookup(this); //todo: I'd prefer not to have to instantiate to do this, move the aliasing funcitonality elsewhere
        foreach (var block in module.Declarations.ImplementBlocks)
        {
            foreach (var function in block.FunctionDeclarations)
            {
                string[] functionNamespace; 
                var typeSymbol = CreateCanonicalSymbolSegments(thisNamespaceSegments, block.TypeNode.Symbol.Segments);

                if (block.TraitNode is not null)
                {
                    if (lookup.TryGetTrait(
                            module,
                            block.TraitNode.Symbol, 
                            thisNamespaceSegments,
                            out var trait))
                    {
                        functionNamespace = [..typeSymbol, ..trait.Symbol.Segments];
                    }
                    else
                    {
                        //todo: What about partially in scope, that neesd ot be worked around in here somewhere.
                        var traitSymbol = CreateCanonicalSymbolSegments(thisNamespaceSegments, block.TraitNode.Symbol.Segments);
                        functionNamespace = [..typeSymbol, ..traitSymbol];
                    }
                }
                else
                {
                    functionNamespace = typeSymbol;
                }
                
                RegisterFunction(function, functionNamespace, diagnostics);
            }
        }

        foreach (var childModule in module.Declarations.ChildModules)
        {
            RegisterFunctionSymbols(childModule, thisNamespaceSegments, diagnostics);
        }
    }

    private void RegisterFunction(FunctionDeclarationNode function, string[] functionNamespace, Diagnostics diagnostics)
    { 
        string[] canonicalSymbolSegments = [..functionNamespace, function.Signature.Name];
        var canonicalSymbol = new Symbol(canonicalSymbolSegments);
        function.Symbol = canonicalSymbol;
                
        if (Functions.TryGet(canonicalSymbol, out var otherDeclaration))
        {
            diagnostics.Duplicate(function, otherDeclaration.Span);
        }
        else
        {
            Functions.Add(canonicalSymbol, function);
        }
    }
    
    public void RegisterTypeSymbols(ModuleDeclarationNode module, string[] parentNamespaceSegments, Diagnostics diagnostics)
    {
        var thisNamespaceSegments = CreateCanonicalSymbolSegments(parentNamespaceSegments, module.Symbol.Segments);
        var canonicalModuleSymbol = new Symbol(thisNamespaceSegments);
        module.Symbol = canonicalModuleSymbol;

        foreach (var structDeclaration in module.Declarations.Structs)
        {
            var canonicalSymbolSegments = CreateCanonicalSymbolSegments(thisNamespaceSegments, structDeclaration.Symbol.Segments);
            var canonicalSymbol = new Symbol(canonicalSymbolSegments);
            structDeclaration.UpdateSymbol(canonicalSymbol);
            if (NominalTypes.TryGet(canonicalSymbol, out var otherDeclaration))
            {
                diagnostics.Duplicate(structDeclaration, otherDeclaration.Span);
            }
            else
            {
                NominalTypes.Add(canonicalSymbol, structDeclaration.Type);
                _structs.Add(canonicalSymbol, structDeclaration);
            }
        }

        foreach (var traitDeclaration in module.Declarations.Traits)
        {
            var canonicalSymbolSegments = CreateCanonicalSymbolSegments(thisNamespaceSegments, traitDeclaration.Symbol.Segments);
            var canonicalSymbol = new Symbol(canonicalSymbolSegments);
            traitDeclaration.UpdateSymbol(canonicalSymbol);
            
            if (Traits.TryGet(canonicalSymbol, out var otherDeclaration))
            {
                diagnostics.Duplicate(traitDeclaration, otherDeclaration.Span);
            }
            else
            {
                Traits.Add(canonicalSymbol, traitDeclaration);
            }
        }
        
        foreach (var childModule in module.Declarations.ChildModules)
        {
            RegisterTypeSymbols(childModule, thisNamespaceSegments, diagnostics);
        }
    }

    public static string[] CreateCanonicalSymbolSegments(string[] parentNamespaceSegments, string[] thisNamespaceSegments)
    {
        if (parentNamespaceSegments.Length == 0)
        {
            return thisNamespaceSegments;
        }

        if (thisNamespaceSegments.Length == 0)
        {
            return parentNamespaceSegments;
        }

        var maxOverlap = Math.Min(parentNamespaceSegments.Length, thisNamespaceSegments.Length);
        var overlapSize = 0;

        for (var candidateOverlap = maxOverlap; candidateOverlap > 0; candidateOverlap--)
        {
            var matches = true;

            for (var i = 0; i < candidateOverlap; i++)
            {
                var parentIndex = parentNamespaceSegments.Length - candidateOverlap + i;
                if (!string.Equals(parentNamespaceSegments[parentIndex], thisNamespaceSegments[i], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                overlapSize = candidateOverlap;
                break;
            }
        }

        return [.. parentNamespaceSegments, .. thisNamespaceSegments[overlapSize..]];
    }

    public void RegisterAliasSymbols(ModuleDeclarationNode module, Diagnostics diagnostics)
    {
        var localAliases = new Dictionary<string, AliasDeclarationNode>();
        foreach (var alias in module.Declarations.Aliases)
        {
            if (localAliases.TryGetValue(alias.Name, out var aliasDeclarationNode))
            {
                diagnostics.Duplicate(alias, aliasDeclarationNode.Span);
            }

            localAliases.TryAdd(alias.Name, alias);
        }
        _localAliases[module.Symbol] =  localAliases; // modules are already verified unique

        foreach (var childModule in module.Declarations.ChildModules)
        {
            RegisterAliasSymbols(childModule, diagnostics);
        }
    }

    public void BuildAliasContexts(ModuleDeclarationNode module, Dictionary<string, Symbol> inheritedAliases, Diagnostics diagnostics)
    {
        var canonizedAliases = new Dictionary<string, Symbol>(inheritedAliases);

        if (_localAliases.TryGetValue(module.Symbol, out var localAliases))
        {
            foreach (var registeredAlias in localAliases)
            {
                if (canonizedAliases.ContainsKey(registeredAlias.Key))
                {
                    // Aliases are already verified unique named in scope.
                    // An alias will only already be registered here if it was eagerly canonized during another alias' resolution  
                    continue;
                }
                
                RegisterCanonizedAlias(registeredAlias.Value, localAliases, canonizedAliases, [], diagnostics);
            }
        }
        
        _moduleAliases[module.Symbol] = canonizedAliases;

        foreach (var childModule in module.Declarations.ChildModules)
        {
            BuildAliasContexts(childModule, canonizedAliases, diagnostics);
        }
    }

    private void RegisterCanonizedAlias(AliasDeclarationNode alias,
        Dictionary<string, AliasDeclarationNode> localAliases, 
        Dictionary<string, Symbol> canonicalAliases,
        HashSet<string> visitedAliases, 
        Diagnostics diagnostics)
    {
        if (!visitedAliases.Add(alias.Name))
        {
            diagnostics.CircularReference(alias, visitedAliases);
            return;
        }
        
        var resolvingAliasSymbolSegments = alias.AliasingSymbol;

        var aliasSubstituted = false;
        var canonizedSymbolSegments = new List<string>();
        
        var replacementCandidate = resolvingAliasSymbolSegments.Segments[0];

        if (canonicalAliases.TryGetValue(replacementCandidate, out var foundCanonicalAlias))
        {
            canonizedSymbolSegments.AddRange(foundCanonicalAlias.Segments);
            aliasSubstituted = true;
        }
        else if (localAliases.TryGetValue(replacementCandidate, out var foundLocalAlias))
        {
            RegisterCanonizedAlias(foundLocalAlias, localAliases, canonicalAliases, visitedAliases, diagnostics);
            
            if (canonicalAliases.TryGetValue(replacementCandidate, out foundCanonicalAlias))
            {
                canonizedSymbolSegments.AddRange(foundCanonicalAlias.Segments);
                aliasSubstituted = true;
            }
        }
        
        canonizedSymbolSegments.AddRange(
            aliasSubstituted ? alias.AliasingSymbol.Segments[1..] : alias.AliasingSymbol.Segments);
        
        var canonicalSymbol = Symbol.From(canonizedSymbolSegments);
        alias.UpdateAliasingSymbol(canonicalSymbol);
        canonicalAliases[alias.Name] = canonicalSymbol;
        visitedAliases.Remove(alias.Name);
    }
    
    public void RegisterModules(ModuleDeclarationNode module, Diagnostics diagnostics)
    {
        if (Modules.TryGet(module.Symbol, out var otherModule))
        {
            diagnostics.Duplicate(module, otherModule.Span);
        }
        else
        {
            Modules.Add(module.Symbol, module);
        }

        foreach (var childModule in module.Declarations.ChildModules)
        {
            RegisterModules(childModule,  diagnostics);
        }
    }
}

public class SymbolList<T> where T : class
{
    private readonly Dictionary<string, HashSet<Symbol>> _candidateSymbolsForMemberNamedElement = new();
    private readonly Dictionary<Symbol, T> _symbols = new();
    private readonly Dictionary<T, Symbol> _canonicalNames = new(ReferenceEqualityComparer.Instance);
    
    public IReadOnlyDictionary<Symbol, T> Symbols => _symbols;
    public IReadOnlyDictionary<T, Symbol> CanonicalNames => _canonicalNames;
    public IReadOnlyDictionary<string, HashSet<Symbol>> CandidateSymbolsForMemberNamedElement => _candidateSymbolsForMemberNamedElement;
    
    public void Add(Symbol canonicalName, T node)
    {
        _symbols.Add(canonicalName, node);
        _canonicalNames.Add(node, canonicalName);

        if (_candidateSymbolsForMemberNamedElement.TryGetValue(canonicalName.MemberName, out var value))
        {
            _ = value.Add(canonicalName);
        }
        else
        {
            _candidateSymbolsForMemberNamedElement.Add(canonicalName.MemberName, [canonicalName]);
        }
    }
    
    public bool TryGet(Symbol canonicalName, [NotNullWhen(true)] out T? node) => _symbols.TryGetValue(canonicalName, out node);
}