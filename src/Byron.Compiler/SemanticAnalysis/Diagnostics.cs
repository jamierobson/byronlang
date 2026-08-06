using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Lexer;

namespace Byron.Compiler.SemanticAnalysis;

public class Diagnostics
{
    public bool HasErrors => _diagnosticMessages.Count > 0;
    
    private readonly List<string> _diagnosticMessages = [];
    public IReadOnlyList<string> DiagnosticMessages => _diagnosticMessages;
    
    public void UndeclaredType(TypeNode type, string? usage = null)
    {
        if (string.IsNullOrWhiteSpace(usage))
        {
            _diagnosticMessages.Add($"The type {type} could not be found at {type.Span}. Are you missing an import?");
        }
        else
        {
            _diagnosticMessages.Add($"The {usage} type {type} could not be found at {type.Span}. Are you missing an import?");
        }
    }
    
    public void Duplicate(FunctionDeclarationNode node, SourceSpan duplicateSpan) => _diagnosticMessages.Add($"Duplicate function declaration {node.CanonicalName()} at {node.Span}. Originally declared at {duplicateSpan}");
    public void Duplicate(StructDeclarationNode node, SourceSpan duplicateSpan) => _diagnosticMessages.Add($"Duplicate struct declaration {node.CanonicalName()} at {node.Span}. Originally declared at {duplicateSpan}");
    public void TypeMismatch(TypeNode leftType, TypeNode rightType) => _diagnosticMessages.Add($"Cannot convert {leftType.CanonicalName()} to type {rightType.CanonicalName()} at {leftType.Span}");
    public void MissingMember(string canonicalName, MemberAccessExpressionNode expression) => _diagnosticMessages.Add($"{canonicalName} does not contain field {expression.MemberName} at {expression.Span}");
    public void InvalidStructName(StructDeclarationNode structDeclaration, string canonicalName) => _diagnosticMessages.Add($"Invalid struct name {structDeclaration.Name} at {structDeclaration.Span}");
    public void CircularReference(string canonicalName, SourceSpan sourceSpan) => _diagnosticMessages.Add($"Circular reference in type {canonicalName} at {sourceSpan}");
    public void UndeclaredVariable(VariableExpressionNode variableExpression) => _diagnosticMessages.Add($"Cannot resolve symbol {variableExpression.Name} at {variableExpression.Span}");
    public void UndeclaredFunction(VariableExpressionNode variableExpression) => _diagnosticMessages.Add($"Cannot resolve function {variableExpression.Name} at {variableExpression.Span}");
    public void InvalidArgumentCount(CallExpressionNode callExpression, FunctionSymbol function) => _diagnosticMessages.Add($"{function.Name} has {function.Parameters.Count} parameter(s) but is invoked with {callExpression.Arguments.Count} arguments at {callExpression.Span}");
    public void InvalidArgument(string argumentType, string parameterType, SourceSpan span) => _diagnosticMessages.Add($"Argument type {argumentType} is not assignable to parameter type {parameterType} at {span}");
}