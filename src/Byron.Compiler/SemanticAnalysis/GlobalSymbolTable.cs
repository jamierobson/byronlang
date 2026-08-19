using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class GlobalSymbolTable
{
    public readonly IReadOnlyDictionary<string, PrimitiveTypeSymbol> Primitives = SeedPrimitives();

    public readonly SymbolList<FunctionSignatureNode> Functions = new();
    public readonly SymbolList<TraitDeclarationNode> Traits = new();
    public readonly SymbolList<StructDeclarationNode> Structs = new();
    public readonly SymbolList<NominalTypeNode> NominalTypes = new();
    
    public void RegisterModuleSymbols(ModuleDeclarationNode module, string[] parentNamespaceSegments, Diagnostics diagnostics)
    {
        var thisNamespaceSegments = CreateCanonicalSymbolSegments(parentNamespaceSegments, module.Symbol.Segments);

        foreach (var structDeclaration in module.Declarations.Structs)
        {
            var canonicalSymbol = CreateCanonicalSymbolSegments(thisNamespaceSegments, structDeclaration.Symbol.Segments);
            var canonicalName = string.Join('.', canonicalSymbol);
            if (Structs.TryGet(canonicalName, out var otherDeclaration))
            {
                diagnostics.Duplicate(structDeclaration, otherDeclaration.Span);
            }
            else
            {
                Structs.Add(canonicalName, structDeclaration);
                NominalTypes.Add(canonicalName, structDeclaration.Type);
            }
        }

        foreach (var traitDeclaration in module.Declarations.Traits)
        {
            var canonicalSymbol = CreateCanonicalSymbolSegments(thisNamespaceSegments, traitDeclaration.Symbol.Segments);
            var canonicalName = string.Join('.', canonicalSymbol);
            
            if (Traits.TryGet(canonicalName, out var otherDeclaration))
            {
                diagnostics.Duplicate(traitDeclaration, otherDeclaration.Span);
            }
            else
            {
                Traits.Add(canonicalName, traitDeclaration);
            }
        }

        foreach (var function in module.Declarations.Functions)
        {
            string[] canonicalSymbol = [..thisNamespaceSegments, function.Signature.Name];
            var canonicalName = string.Join('.', canonicalSymbol);
            
            
            if (Functions.TryGet(canonicalName, out var otherDeclaration))
            {
                diagnostics.Duplicate(function, otherDeclaration.Span);
            }
            else
            {
                Functions.Add(canonicalName, function.Signature);
            }
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
                
                string[] canonicalSymbol = [..functionNamespace, function.Signature.Name];
                var canonicalName = string.Join('.', canonicalSymbol);
                
                if (Functions.TryGet(canonicalName, out var otherDeclaration))
                {
                    diagnostics.Duplicate(function, otherDeclaration.Span);
                }
                else
                {
                    Functions.Add(canonicalName, function.Signature);
                }
            }
        }
        
        foreach (var childModule in module.Declarations.ChildModules)
        {
            RegisterModuleSymbols(childModule, thisNamespaceSegments, diagnostics);
        }
    }

    private static string[] CreateCanonicalSymbolSegments(string[] parentNamespaceSegments, string[] thisNamespaceSegments)
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
    
    public bool TryResolveStruct(string[] scopeSegments, string[] querySegments, [NotNullWhen(true)] out StructDeclarationNode? structNode)
    {
        //todo: Lots of string.join here will be very inefficient
        var exactKey = string.Join('.', querySegments);
        if (Structs.TryGet(exactKey, out structNode))
        {
            return true;
        }

        for (var i = scopeSegments.Length; i >= 0; i--)
        {
            var prefix = scopeSegments[..i];
            var candidateKey = string.Join('.', prefix.Concat(querySegments));
            if (Structs.TryGet(candidateKey, out structNode))
            {
                return true;
            }
        }

        structNode = null;
        return false;
    }
    
    public bool TryResolveFunction(string[] scopeSegments, string[] pathSegments, string functionName, [NotNullWhen(true)] out FunctionSignatureNode? signature)
    {
        string[] querySegments = [.. pathSegments, functionName];

        var exactKey = string.Join('.', querySegments);
        if (Functions.TryGet(exactKey, out signature))
        {
            return true;
        }
        
        for (var i = scopeSegments.Length; i >= 0; i--)
        {
            var prefix = scopeSegments[..i];
            var candidateKey = string.Join('.', prefix.Concat(querySegments));
            if (Functions.TryGet(candidateKey, out signature))
            {
                return true;
            }
        }

        signature = null;
        return false;
    }
    
    public bool IsValidType(TypeNode typeNode)
    {
        var canonicalName = typeNode.Symbol.ToString();
        return Primitives.ContainsKey(canonicalName) 
               || NominalTypes.TryGet(canonicalName, out _)
               || Structs.TryGet(canonicalName, out _);
    }

    private static Dictionary<string, PrimitiveTypeSymbol> SeedPrimitives()
    {
        return new Dictionary<string, PrimitiveTypeSymbol>
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
    }
}

public class SymbolList<T> where T : class
{
    private readonly Dictionary<string, T> _symbols = new();
    private readonly Dictionary<T, string> _canonicalNames = new(ReferenceEqualityComparer.Instance);
    public IReadOnlyDictionary<string, T> Symbols => _symbols;
    public IReadOnlyDictionary<T, string> CanonicalNames => _canonicalNames;
    
    public void Add(string canonicalName, T node)
    {
        _symbols.Add(canonicalName, node);
        _canonicalNames.Add(node, canonicalName);
    }
    
    public bool TryGet(string canonicalName, [NotNullWhen(true)] out T? node) => _symbols.TryGetValue(canonicalName, out node);
}