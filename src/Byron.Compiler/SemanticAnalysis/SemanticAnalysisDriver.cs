using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class SemanticAnalysisDriver(ProgramNode program)
{
    private readonly Diagnostics _diagnostics = new();
    
    public SemanticAnalysisResult Analyze()
    {
        // var typeRegistry = new TypeRegistry();
        // var functionRegistry = new  FunctionRegistry();
        var typeMap = new TypeMap();
        var scopedSymbolTable = new ScopedSymbolTable();
        var globalSymbolTable = new GlobalSymbolTable();
        
        foreach (var fileModule in program.RootModules)
        {
            globalSymbolTable.RegisterModuleSymbols(fileModule, [], _diagnostics);
        }
        
        if (_diagnostics.HasErrors)
        {
            return SemanticAnalysisResult.Error(program, _diagnostics);
        }
        
        var visitor = new TypeInferenceVisitor(globalSymbolTable, typeMap, scopedSymbolTable, _diagnostics);
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
        foreach (var function in module.Declarations.Functions)
        {
            visitor.VisitFunction(function);
        }

        foreach (var function in module.Declarations.ImplementBlocks.SelectMany(x => x.FunctionDeclarations))
        {
            visitor.VisitFunction(function);
        }

        foreach (var childModule in module.Declarations.ChildModules)
        {
            VisitFunctions(childModule, visitor);
        }
    }
}

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

public class TypeMap
{
    private readonly Dictionary<NodeId, TypeNode> _nodeTypes = new();

    public void SetType(ExpressionNode node, TypeNode type)
    {
        _nodeTypes[node.Id] = type;
    }

    public TypeNode GetType(ExpressionNode node)
    {
        if (_nodeTypes.TryGetValue(node.Id, out var type))
        {
            return type;
        }

        throw new InvalidOperationException($"Node {node.GetType().Name} (Id: {node}) has not been assigned a type at {node.Span}.");
    }

    public bool TryGetType(ExpressionNode node, [NotNullWhen(true)] out TypeNode? type)
    {
        return _nodeTypes.TryGetValue(node.Id, out type);
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