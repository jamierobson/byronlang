using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class SemanticAnalysisDriver(ProgramNode program)
{
    private readonly Diagnostics _diagnostics = new();
    
    public SemanticAnalysisResult Analyze()
    {
        var typeMap = new TypeMap(_diagnostics);
        var scopedSymbolTable = new ScopedSymbolTable();
        var globalSymbolTable = new GlobalSymbolTable();

        globalSymbolTable.Register(program.RootModules, _diagnostics);
        
        if (_diagnostics.HasErrors)
        {
            return SemanticAnalysisResult.Error(program, _diagnostics);
        }
        
        var globalSymbolTableLookup = new GlobalSymbolTableLookup(globalSymbolTable);
        foreach (var fileModule in program.RootModules)
        {
            CanonizeStructDeclarationFields(globalSymbolTableLookup, fileModule, _diagnostics);
            CanonizeImplementBlockTypes(globalSymbolTableLookup, fileModule, _diagnostics);
        }
        
        var canonicalResolvingTypeMap = new CanonicalResolvingTypeMap(typeMap, globalSymbolTableLookup, _diagnostics);
        var typeCoercion = new TypeCoercion(globalSymbolTableLookup, canonicalResolvingTypeMap, _diagnostics);
        var visitor = new TypeInferenceVisitor(globalSymbolTableLookup, canonicalResolvingTypeMap, typeCoercion, scopedSymbolTable, _diagnostics);
        foreach (var module in program.RootModules)
        {
            VisitFunctions(module, visitor);
        }

        if (_diagnostics.HasErrors)
        {
            return SemanticAnalysisResult.Error(program, _diagnostics);
        }

        return SemanticAnalysisResult.Ok(program, globalSymbolTable, typeMap);
    }

    private void VisitFunctions(ModuleDeclarationNode module, TypeInferenceVisitor visitor)
    {
        var moduleScopedFreeFunctionContext = new FunctionDeclarationContext(module, null);
        foreach (var function in module.Declarations.Functions)
        {
            visitor.VisitFunction(function, moduleScopedFreeFunctionContext);
        }

        foreach (var implementBlock in module.Declarations.ImplementBlocks)
        {
            var associatedFunctionContext = new FunctionDeclarationContext(module, implementBlock);
            foreach (var function in implementBlock.FunctionDeclarations)
            {
                visitor.VisitFunction(function, associatedFunctionContext);
            }
        }

        foreach (var childModule in module.Declarations.ChildModules)
        {
            VisitFunctions(childModule, visitor);
        }
    }

    private void CanonizeImplementBlockTypes(
        GlobalSymbolTableLookup globalSymbolTableLookup,
        ModuleDeclarationNode module, 
        Diagnostics diagnostics)
    {
        foreach (var block in module.Declarations.ImplementBlocks)
        {
            if (globalSymbolTableLookup.TryResolveCanonicalType(
                    module, 
                    block.TypeNode,
                    out var canonicalType))
            {
                block.UpdateType((NominalTypeNode)canonicalType);
            }
            else
            {
                diagnostics.UndeclaredType(block.TypeNode, "struct field");
            }
            // todo: Trait node
        }

        foreach (var childModule in module.Declarations.ChildModules)
        {
            CanonizeImplementBlockTypes(globalSymbolTableLookup, childModule, diagnostics);
        }
    }

    private void CanonizeStructDeclarationFields(
        GlobalSymbolTableLookup globalSymbolTableLookup,
        ModuleDeclarationNode module, 
        Diagnostics diagnostics)
    {
        foreach (var field in module.Declarations.Structs.SelectMany(x => x.Fields))
        {
            if (globalSymbolTableLookup.TryResolveCanonicalType(
                    module, 
                    field.Type,
                    out var resolvedFieldType))
            {
                field.Type = resolvedFieldType;
            }
            else
            {
                diagnostics.UndeclaredType(field.Type, "struct field");
            }
        }

        foreach (var childModule in module.Declarations.ChildModules)
        {
            CanonizeStructDeclarationFields(globalSymbolTableLookup, childModule, diagnostics);
        }
    }
}

public record FunctionDeclarationContext(ModuleDeclarationNode Module, ImplementBlockDeclarationNode? ImplementBlock);

public record SemanticAnalysisResult
{
    [MemberNotNullWhen(true, [nameof(GlobalSymbolTable), nameof(TypeMap)])]
    [MemberNotNullWhen(false, nameof(Diagnostics))]
    public bool Success { get; }

    public ProgramNode Ast { get; }
    public TypeMap? TypeMap { get; }
    public Diagnostics? Diagnostics { get; }
    private GlobalSymbolTable? GlobalSymbolTable { get; }
    
    private SemanticAnalysisResult(bool success, ProgramNode ast, GlobalSymbolTable? globalSymbolTable = null, TypeMap? typeMap = null, Diagnostics? diagnostics = null)
    {
        Success = success;
        Ast = ast;
        GlobalSymbolTable = globalSymbolTable;
        TypeMap = typeMap;
        Diagnostics = diagnostics;
    }

    public static SemanticAnalysisResult Ok(ProgramNode ast, GlobalSymbolTable globalSymbolTable, TypeMap typeMap)
    {
        return new SemanticAnalysisResult(
            true, 
            ast,
            globalSymbolTable,
            typeMap: typeMap
            );
    }

    public static SemanticAnalysisResult Error(ProgramNode ast, Diagnostics diagnostics)
    {
        return new SemanticAnalysisResult(
            false, 
            ast,
            diagnostics: diagnostics);
    }
    
    public void Deconstruct(
        out ProgramNode ast,
        out GlobalSymbolTable globalSymbolTable,
        out TypeMap typeMap
        )
    {
        if (!Success)
        {
            throw new ByronSemanticAnalysisException($"Cannot deconstruct the program contents of the {nameof(SemanticAnalysisResult)} when semantic analysis rejected the program", Diagnostics);
        }
        ast = Ast;
        globalSymbolTable = GlobalSymbolTable;
        typeMap = TypeMap;
    }
}

public record LookupSymbol(string Name, TypeNode Type, bool IsMutable);

public class ScopedSymbolTable
{
    private readonly Stack<Dictionary<string, LookupSymbol>> _scopes = new();

    public ScopedSymbolTable()
    {
        EnterScope();
    }

    public void EnterScope() => _scopes.Push(new());
    public void ExitScope() => _scopes.Pop();

    public bool Declare(string name, TypeNode type, bool isMutable)
    {
        return _scopes.Peek().TryAdd(name, new LookupSymbol(name, type, isMutable));
    }

    public bool TryGet(string name, [NotNullWhen(true)] out LookupSymbol? symbol)
    {
        symbol = null;
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out symbol))
            {
                return true;
            }
        }
        return false;
    }
}