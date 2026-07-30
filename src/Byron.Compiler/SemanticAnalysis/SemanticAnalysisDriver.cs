using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST.HighLevel;
namespace Byron.Compiler.SemanticAnalysis;

public class SemanticAnalysisDriver(ProgramNode ast)
{
    public SemanticAnalysisResult Analyze()
    {
        return SemanticAnalysisResult.Ok(ast, new TypeRegistry(), new TypeMap());
    }
}

public record SemanticAnalysisResult
{
    [MemberNotNullWhen(true, [nameof(TypeMap), nameof(TypeRegistry)])]
    [MemberNotNullWhen(false, nameof(Diagonstics))]
    public bool Success { get; }

    public ProgramNode Ast { get; }
    public TypeRegistry? TypeRegistry { get; }
    public TypeMap? TypeMap { get; }
    public Diagnostics? Diagonstics { get; }
    
    private SemanticAnalysisResult(bool success, ProgramNode ast, TypeRegistry? typeRegistry, TypeMap? typeMap, Diagnostics? diagnostics)
    {
        Success = success;
        Ast = ast;
        TypeRegistry = typeRegistry;
        TypeMap = typeMap;
        Diagonstics = diagnostics;
    }

    public static SemanticAnalysisResult Ok(ProgramNode ast, TypeRegistry typeRegistry, TypeMap typeMap)
    {
        return new SemanticAnalysisResult(true, ast, typeRegistry, typeMap, null);
    }

    public static SemanticAnalysisResult Error(ProgramNode ast, Diagnostics diagnostics)
    {
        return new SemanticAnalysisResult(false, ast, null, null, diagnostics);
    }
}
public record TypeRegistry();
public record TypeMap();
public record Diagnostics();