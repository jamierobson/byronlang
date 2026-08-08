using Byron.Compiler.AST;
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
    public void Duplicate(VariableDeclarationNode variableDeclaration) => _diagnosticMessages.Add($"A local variable {variableDeclaration.Name} cannot be declared at {variableDeclaration.Span} because the variable name already has another binding");
    public void TypeMismatch(TypeNode leftType, TypeNode rightType) => _diagnosticMessages.Add($"Cannot convert {leftType.CanonicalName()} to type {rightType.CanonicalName()} at {rightType.Span}");
    public void TypeMismatch(TypeNode leftType, string rightType) => _diagnosticMessages.Add($"Cannot convert {leftType.CanonicalName()} to type {rightType} at {leftType.Span}");
    public void OutOfRange(IntegerLiteralNode literal, TypeNode type) => _diagnosticMessages.Add($"{literal.Value} is out of range for type {type.CanonicalName()} at {literal.Span}");
    public void MissingMember(string canonicalName, MemberAccessExpressionNode expression) => _diagnosticMessages.Add($"{canonicalName} does not contain field {expression.MemberName} at {expression.Span}");
    public void MissingMember(string canonicalName, StructFieldInitializerNode initializer) => _diagnosticMessages.Add($"{canonicalName} does not contain field {initializer.FieldName} at {initializer.Span}");
    public void InvalidStructName(StructDeclarationNode structDeclaration, string canonicalName) => _diagnosticMessages.Add($"Invalid struct name {structDeclaration.Name} at {structDeclaration.Span}");
    public void CircularReference(string canonicalName, SourceSpan sourceSpan) => _diagnosticMessages.Add($"Circular reference in type {canonicalName} at {sourceSpan}");
    public void UndeclaredVariable(VariableExpressionNode variableExpression) => _diagnosticMessages.Add($"Cannot resolve symbol {variableExpression.Name} at {variableExpression.Span}");
    public void UndeclaredFunction(VariableExpressionNode variableExpression) => _diagnosticMessages.Add($"Cannot resolve function {variableExpression.Name} at {variableExpression.Span}");
    public void InvalidArgumentCount(CallExpressionNode callExpression, FunctionSymbol function) => _diagnosticMessages.Add($"{function.Name} has {function.Parameters.Count} parameter(s) but is invoked with {callExpression.Arguments.Count} arguments at {callExpression.Span}");
    public void InvalidArgument(string argumentType, string parameterType, SourceSpan span) => _diagnosticMessages.Add($"Argument type {argumentType} is not assignable to parameter type {parameterType} at {span}");
    public void InvalidMutation(VariableExpressionNode variable, SourceSpan typeSpan) => _diagnosticMessages.Add($"Variable {variable.Name} is is mutated at {variable.Span} but declared immutable at {typeSpan}");
    public void InvalidUnaryOperation(UnaryExpressionNode unary, TypeNode operandType) => _diagnosticMessages.Add($"Cannot apply {unary.Operator.ToLexeme()} operator to type {operandType.CanonicalName()} at {unary.Span}");
}