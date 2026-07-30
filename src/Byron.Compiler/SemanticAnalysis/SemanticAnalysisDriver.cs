using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;
namespace Byron.Compiler.SemanticAnalysis;

public class SemanticAnalysisDriver(ProgramNode ast)
{
    private FunctionRegistry _functionRegistry = new();
    private TypeMap _typeMap = new();
    private Diagnostics _diagnostics = new();
    
    public SemanticAnalysisResult Analyze()
    {
        var typeRegistry = RegisterStructs();
        var functionRegistry = RegisterFunctions();
        
        var visitor = new TypeInferenceVisitor(typeRegistry, functionRegistry, _typeMap, _diagnostics);
        foreach (var function in ast.Declarations.OfType<FunctionDeclarationNode>())
        {
            visitor.Analyze(function, _diagnostics);
        }
        
        if (_diagnostics.HasErrors)
        {
            return SemanticAnalysisResult.Error(ast, _diagnostics);
        }
        return SemanticAnalysisResult.Ok(ast, typeRegistry, functionRegistry, _typeMap);
        
    }

    private TypeRegistry RegisterStructs()
    {
        var typeRegistry = new TypeRegistry();
        foreach (var structDeclarationNode in ast.Declarations.OfType<StructDeclarationNode>())
        {
            var canonicalName = structDeclarationNode.CanonicalName();
            if (typeRegistry.Declarations.ContainsKey(canonicalName))
            {
                _diagnostics.Add($"Struct declaration already exists for {canonicalName}");
            }
            typeRegistry.Register(structDeclarationNode);
        }

        return typeRegistry;
    }

    private FunctionRegistry RegisterFunctions()
    {
        var  functionRegistry = new FunctionRegistry();

        foreach (var functionDeclarationNode in ast.Declarations.OfType<FunctionDeclarationNode>())
        {
            var canonicalName = functionDeclarationNode.CanonicalName();
            if (functionRegistry.Declarations.ContainsKey(canonicalName))
            {
                _diagnostics.Add($"Function declaration already exists for {canonicalName}");
            }
            functionRegistry.Register(functionDeclarationNode);
        }
        
        return functionRegistry;
    }
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

public class FunctionRegistry
{
    private readonly Dictionary<string, FunctionDeclarationNode> _declarations = [];
    public IReadOnlyDictionary<string, FunctionDeclarationNode> Declarations => _declarations;

    public void Register(FunctionDeclarationNode functionDeclarationNode)
    {
        _declarations.Add(functionDeclarationNode.CanonicalName(), functionDeclarationNode);
    }
    
    public bool TryGetFunction(string canonicalName, [NotNullWhen(true)] out FunctionDeclarationNode? function)
    {
        return _declarations.TryGetValue(canonicalName, out function);
    }
}

public class TypeRegistry
{
    private readonly Dictionary<string, StructDeclarationNode> _declarations = [];
    public IReadOnlyDictionary<string, StructDeclarationNode> Declarations => _declarations; 
    public void Register(StructDeclarationNode structDeclarationNode)
    {
        _declarations.Add(structDeclarationNode.CanonicalName(), structDeclarationNode);
    }

    public bool TryGetStruct(string canonicalName, [NotNullWhen(true)]out StructDeclarationNode? @struct)
    {
        return _declarations.TryGetValue(canonicalName, out @struct);
    }
    
    // public bool TryGetStructInScope(List<string> modulePath, string shortName, [NotNullWhen(true)] out StructDeclarationNode? structDecl)
    // {
    //     var canonicalName = CanonicalNames.InModule(modulePath, shortName);
    //     return 
    //         _declarations.TryGetValue(canonicalName, out structDecl) 
    //         || _declarations.TryGetValue(shortName, out structDecl);
    // }

    public bool TryGetFieldType(string canonicalName, string fieldName, [NotNullWhen(true)] out TypeNode? fieldType)
    {
        fieldType = null;
        if (_declarations.TryGetValue(canonicalName, out var structDeclaration))
        {
            foreach (var field in structDeclaration.Fields)
            {
                if (field.Name == fieldName)
                {
                    fieldType = field.Type;
                    return true;
                }
            }
        }
        
        return false;
    }
}

public class TypeMap
{
    private readonly Dictionary<ExpressionNode, TypeNode> _nodeTypes = new(ReferenceEqualityComparer.Instance);

    public void SetType(ExpressionNode node, TypeNode type)
    {
        _nodeTypes[node] = type;
    }

    public TypeNode GetType(ExpressionNode node)
    {
        if (_nodeTypes.TryGetValue(node, out var type))
        {
            return type;
        }

        throw new InvalidOperationException($"Node {node.GetType().Name} (Id: {node}) has not been assigned a type.");
    }

    public bool TryGetType(ExpressionNode node, [NotNullWhen(true)] out TypeNode? type)
    {
        return _nodeTypes.TryGetValue(node, out type);
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

public class Diagnostics
{
    public bool HasErrors => _diagnosticMessages.Count > 0;
    
    private readonly List<string> _diagnosticMessages = [];
    public IReadOnlyList<string> DiagnosticMessages => _diagnosticMessages;
    
    public void Add(string message) => _diagnosticMessages.Add(message);
}