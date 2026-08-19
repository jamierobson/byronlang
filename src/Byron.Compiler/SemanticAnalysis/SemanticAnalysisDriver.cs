using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class SemanticAnalysisDriver(ProgramNode program)
{
    private readonly Diagnostics _diagnostics = new();
    
    public SemanticAnalysisResult Analyze()
    {
        var typeMap = new TypeMap();
        var scopedSymbolTable = new ScopedSymbolTable();
        var globalSymbolTable = new GlobalSymbolTable();
        
        foreach (var fileModule in program.RootModules)
        {
            globalSymbolTable.RegisterTypeSymbols(fileModule, [], _diagnostics);
        }

        foreach (var fileModule in program.RootModules)
        {
            globalSymbolTable.RegisterFunctionSymbols(fileModule, [], _diagnostics);
        }
        
        if (_diagnostics.HasErrors)
        {
            return SemanticAnalysisResult.Error(program, _diagnostics);
        }
        
        var visitor = new TypeInferenceVisitor(new GlobalSymbolTableLookup(globalSymbolTable), typeMap, scopedSymbolTable, _diagnostics);
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
}

public record FunctionDeclarationContext(ModuleDeclarationNode Module, ImplementBlockDeclarationNode? ImplementBlock);

public record SemanticAnalysisResult
{
    // [MemberNotNullWhen(true, [nameof(TypeMap), nameof(TypeRegistry), nameof(FunctionRegistry)])]
    [MemberNotNullWhen(true, [nameof(GlobalSymbolTable), nameof(TypeMap)])]
    [MemberNotNullWhen(false, nameof(Diagnostics))]
    public bool Success { get; }

    public ProgramNode Ast { get; }
    // public TypeRegistry? TypeRegistry { get; }
    public TypeMap? TypeMap { get; }
    // public FunctionRegistry? FunctionRegistry { get; }
    public Diagnostics? Diagnostics { get; }
    private GlobalSymbolTable? GlobalSymbolTable { get; }
    
    // private SemanticAnalysisResult(bool success, ProgramNode ast, TypeRegistry? typeRegistry = null, FunctionRegistry? functionRegistry = null, TypeMap? typeMap = null, Diagnostics? diagnostics = null)
    private SemanticAnalysisResult(bool success, ProgramNode ast, GlobalSymbolTable? globalSymbolTable = null, TypeMap? typeMap = null, Diagnostics? diagnostics = null)
    {
        Success = success;
        Ast = ast;
        GlobalSymbolTable = globalSymbolTable;
        // TypeRegistry = typeRegistry;
        TypeMap = typeMap;
        // FunctionRegistry = functionRegistry;
        Diagnostics = diagnostics;
    }

    // public static SemanticAnalysisResult Ok(ProgramNode ast, TypeRegistry typeRegistry, FunctionRegistry functionRegistry, TypeMap typeMap)
    public static SemanticAnalysisResult Ok(ProgramNode ast, GlobalSymbolTable globalSymbolTable, TypeMap typeMap)
    {
        return new SemanticAnalysisResult(
            true, 
            ast,
            globalSymbolTable,
            // typeRegistry: typeRegistry,
            typeMap: typeMap
            // functionRegistry: functionRegistry
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
        // out TypeRegistry typeRegistry,
        out TypeMap typeMap
        // out FunctionRegistry functionRegistry
        )
    {
        if (!Success)
        {
            throw new InvalidOperationException($"Cannot deconstruct the program contents of the {nameof(SemanticAnalysisResult)} when semantic analysis rejected the program");
        }
        ast = Ast;
        globalSymbolTable = GlobalSymbolTable;
        // typeRegistry = TypeRegistry;
        typeMap = TypeMap;
        // functionRegistry = FunctionRegistry;
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