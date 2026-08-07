using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class SemanticAnalysisDriver(ProgramNode program)
{
    private TypeMap _typeMap = new();
    private Diagnostics _diagnostics = new();
    
    public SemanticAnalysisResult Analyze()
    {
        
        var typeRegistry = new TypeRegistry();
        var functionRegistry = new  FunctionRegistry();
        var typeMap = new TypeMap();
        var symbolTable = new SymbolTable();
        
        var typeResolver = new TypeResolver(typeRegistry, program.Declarations.OfType<StructDeclarationNode>(), _diagnostics);
        typeResolver.Resolve();
        
        var functionDeclarations = program.Declarations.OfType<FunctionDeclarationNode>().ToList();
        
        var functionResolver = new FunctionResolver(functionRegistry, typeRegistry, functionDeclarations, _diagnostics);
        functionResolver.Resolve();
        
        if (_diagnostics.HasErrors)
        {
            return SemanticAnalysisResult.Error(program, _diagnostics);
        }
        
        var visitor = new TypeInferenceVisitor(typeRegistry, functionRegistry, typeMap, symbolTable, _diagnostics);

        foreach (var function in functionDeclarations)
        {
            visitor.VisitFunction(function);
        }

        if (_diagnostics.HasErrors)
        {
            return SemanticAnalysisResult.Error(program, _diagnostics);
        }

        return SemanticAnalysisResult.Ok(program, typeRegistry, functionRegistry, typeMap);
        
        
        
        
        
        
        
        
        //
        //
        // var typeRegistry = RegisterStructs();
        // var functionRegistry = RegisterFunctions();
        //
        // var visitor = new TypeInferenceVisitor(typeRegistry, functionRegistry, _typeMap, _diagnostics);
        // foreach (var function in program.Declarations.OfType<FunctionDeclarationNode>())
        // {
        //     visitor.Analyze(function, _diagnostics);
        // }
        //
        // if (_diagnostics.HasErrors)
        // {
        //     return SemanticAnalysisResult.Error(program, _diagnostics);
        // }
        // return SemanticAnalysisResult.Ok(program, typeRegistry, functionRegistry, _typeMap);
        
    }

    // private TypeRegistry RegisterStructs()
    // {
    //     var typeRegistry = new TypeRegistry();
    //     foreach (var structDeclarationNode in program.Declarations.OfType<StructDeclarationNode>())
    //     {
    //         var canonicalName = structDeclarationNode.CanonicalName();
    //         if (typeRegistry.IsValidType(canonicalName))
    //         {
    //             _diagnostics.Add($"Struct declaration already exists for {structDeclarationNode.Name} in the {( structDeclarationNode.ModulePath.Count > 0 ? $"{string.Join(".", structDeclarationNode)}" : "root" )} module", structDeclarationNode.Span);
    //             continue;
    //         }
    //         typeRegistry.Register(structDeclarationNode);
    //     }
    //
    //     return typeRegistry;
    // }
    //
    // private FunctionRegistry RegisterFunctions()
    // {
    //     var  functionRegistry = new FunctionRegistry();
    //
    //     foreach (var functionDeclarationNode in program.Declarations.OfType<FunctionDeclarationNode>())
    //     {
    //         var canonicalName = functionDeclarationNode.CanonicalName();
    //         if (functionRegistry.Declarations.ContainsKey(canonicalName))
    //         {
    //             _diagnostics.Add($"Function declaration already exists for {canonicalName}", functionDeclarationNode.Span);
    //             continue;
    //         }
    //         functionRegistry.Register(functionDeclarationNode);
    //     }
    //     
    //     return functionRegistry;
    // }
}

public record SemanticAnalysisResult
{
    [MemberNotNullWhen(true, [nameof(TypeMap), nameof(TypeRegistry), nameof(FunctionRegistry)])]
    [MemberNotNullWhen(false, nameof(Diagnostics))]
    public bool Success { get; }

    public ProgramNode Ast { get; }
    public TypeRegistry? TypeRegistry { get; }
    public TypeMap? TypeMap { get; }
    public FunctionRegistry? FunctionRegistry { get; }
    public Diagnostics? Diagnostics { get; }
    
    private SemanticAnalysisResult(bool success, ProgramNode ast, TypeRegistry? typeRegistry = null, FunctionRegistry? functionRegistry = null, TypeMap? typeMap = null, Diagnostics? diagnostics = null)
    {
        Success = success;
        Ast = ast;
        TypeRegistry = typeRegistry;
        TypeMap = typeMap;
        FunctionRegistry = functionRegistry;
        Diagnostics = diagnostics;
    }

    public static SemanticAnalysisResult Ok(ProgramNode ast, TypeRegistry typeRegistry, FunctionRegistry functionRegistry, TypeMap typeMap)
    {
        return new SemanticAnalysisResult(
            true, 
            ast, 
            typeRegistry: typeRegistry,
            typeMap: typeMap,
            functionRegistry: functionRegistry);
    }

    public static SemanticAnalysisResult Error(ProgramNode ast, Diagnostics diagnostics)
    {
        return new SemanticAnalysisResult(
            false, 
            ast,
            diagnostics: diagnostics);
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

public record Symbol(string Name, TypeNode Type, bool IsMutable);

public class SymbolTable
{
    private readonly Stack<Dictionary<string, Symbol>> _scopes = new();

    public SymbolTable()
    {
        EnterScope();
    }

    public void EnterScope() => _scopes.Push(new());
    public void ExitScope() => _scopes.Pop();

    public bool Declare(string name, TypeNode type, bool isMutable)
    {
        return _scopes.Peek().TryAdd(name, new Symbol(name, type, isMutable));
    }

    public bool TryGet(string name, [NotNullWhen(true)] out Symbol? symbol)
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