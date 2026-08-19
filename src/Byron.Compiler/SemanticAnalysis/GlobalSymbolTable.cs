using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class GlobalSymbolTable
{
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
    
    public void RegisterFunctionSymbols(ModuleDeclarationNode module, string[] parentNamespaceSegments, Diagnostics diagnostics)
    {
        var thisNamespaceSegments = CreateCanonicalSymbolSegments(parentNamespaceSegments, module.Symbol.Segments);
        foreach (var function in module.Declarations.Functions)
        {
            RegisterFunction(function, thisNamespaceSegments, diagnostics);
        }

        foreach (var block in module.Declarations.ImplementBlocks)
        {
            foreach (var function in block.FunctionDeclarations)
            {
                string[] functionNamespace; 
                var typeSymbol = CreateCanonicalSymbolSegments(thisNamespaceSegments, block.TypeNode.Symbol.Segments);

                if (block.TraitNode is not null)
                {
                    var traitSymbol = CreateCanonicalSymbolSegments(thisNamespaceSegments, block.TraitNode.Symbol.Segments);
                    functionNamespace = [..typeSymbol, ..traitSymbol];
                }
                else
                {
                    functionNamespace = typeSymbol;
                }
                
                RegisterFunction(function, functionNamespace, diagnostics);
            }
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
        
        if (Modules.TryGet(canonicalModuleSymbol, out var otherModule))
        {
            diagnostics.Duplicate(module, otherModule.Span);
        }
        else
        {
            Modules.Add(canonicalModuleSymbol, module);
        }

        foreach (var structDeclaration in module.Declarations.Structs)
        {
            var canonicalSymbolSegments = CreateCanonicalSymbolSegments(thisNamespaceSegments, structDeclaration.Symbol.Segments);
            var canonicalSymbol = new Symbol(canonicalSymbolSegments);
            structDeclaration.Symbol = canonicalSymbol;
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
            traitDeclaration.Symbol = canonicalSymbol;
            
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