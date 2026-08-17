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
            _diagnosticMessages.Add($"The type {type.CanonicalName} could not be found at {type.Span}. Are you missing an import?");
        }
        else
        {
            _diagnosticMessages.Add($"The {usage} type {type.CanonicalName} could not be found at {type.Span}. Are you missing an import?");
        }
    }
    
    private void Add(string message) => _diagnosticMessages.Add(message);
    
    public void Duplicate(FunctionDeclarationNode node, SourceSpan duplicateSpan) => Add($"Duplicate function declaration {node.CanonicalName} at {node.Span}. Originally declared at {duplicateSpan}");
    public void Duplicate(StructDeclarationNode node, SourceSpan duplicateSpan) => Add($"Duplicate struct declaration {node.CanonicalName} at {node.Span}. Originally declared at {duplicateSpan}");
    public void Duplicate(VariableDeclarationNode variableDeclaration) => Add($"A local variable {variableDeclaration.Name} cannot be declared at {variableDeclaration.Span} because the variable name already has another binding");
    public void TypeMismatch(TypeNode leftType, TypeNode rightType) => Add($"Cannot convert {leftType.CanonicalName} to type {rightType.CanonicalName} at {rightType.Span}");
    public void TypeMismatch(TypeNode leftType, string rightType) => Add($"Cannot convert {leftType.CanonicalName} to type {rightType} at {leftType.Span}");
    public void OutOfRange(IntegerLiteralNode literal, TypeNode type) => Add($"{literal.Value} is out of range for type {type.CanonicalName} at {literal.Span}");
    public void MissingMember(string canonicalName, MemberAccessExpressionNode expression) => Add($"{canonicalName} does not contain field {expression.MemberName} at {expression.Span}");
    public void MissingMember(CanonicalName canonicalName, MemberAccessExpressionNode expression) => Add($"{canonicalName} does not contain field {expression.MemberName} at {expression.Span}");
    public void MissingMember(string canonicalName, StructFieldInitializerNode initializer) => Add($"{canonicalName} does not contain field {initializer.FieldName} at {initializer.Span}");
    public void MissingMember(CanonicalName canonicalName, StructFieldInitializerNode initializer) => Add($"{canonicalName} does not contain field {initializer.FieldName} at {initializer.Span}");
    public void InvalidStructName(StructDeclarationNode structDeclaration, string canonicalName) => Add($"Invalid struct name {structDeclaration.Name} at {structDeclaration.Span}");
    public void CircularReference(CanonicalName canonicalName, SourceSpan sourceSpan) => Add($"Circular reference in type {canonicalName} at {sourceSpan}");
    public void UndeclaredVariable(VariableExpressionNode variableExpression) => Add($"Cannot resolve symbol {variableExpression.Name} at {variableExpression.Span}");
    public void UndeclaredFunction(string functionName, SourceSpan sourceSpan) => Add($"Cannot resolve function {functionName} at {sourceSpan}");
    public void InvalidArgumentCount(CallExpressionNode callExpression, FunctionSymbol function) => Add($"{function.Name} has {function.Parameters.Count} parameter(s) but is invoked with {callExpression.Arguments.Count} arguments at {callExpression.Span}");
    public void InvalidArgument(CanonicalName argumentType, CanonicalName parameterType, CanonicalName function, SourceSpan span) => Add($"Argument type {argumentType} is not assignable to parameter type {parameterType} in function {function}at {span}");
    public void InvalidMutation(VariableExpressionNode variable, SourceSpan typeSpan) => Add($"Variable {variable.Name} is is mutated at {variable.Span} but declared immutable at {typeSpan}");
    public void InvalidUnaryOperation(UnaryExpressionNode unary, TypeNode operandType) => Add($"Cannot apply {unary.Operator.ToLexeme()} operator to type {operandType.CanonicalName} at {unary.Span}");
    public void InvalidDereference(DereferenceExpressionNode dereference, TypeNode targetType) => Add($"Cannot dereference a non-reference type {targetType.CanonicalName} at {dereference.Span}");
    public void InvalidSelfArgument(FunctionSymbol function, SourceSpan callSiteSpan) => Add($"The function {function.CanonicalName} does not expose a first argument self at {callSiteSpan}"); 
    public void InvalidSelfArgument(FunctionDeclarationNode function, ParameterNode parameter) => Add($"{parameter.Name} must be the first parameter declared in the function {function.CanonicalName} at {function.Span}"); 
    public void InvalidSelfArgument(string parameterType, string expectedType, FunctionDeclarationNode function) => Add($"The self parameter of function {function.CanonicalName} should be of type {expectedType}, but was declared as {parameterType} at {function.Span}");
    public void InvalidCast(Symbol symbol, TypeNode targetType, SourceSpan span) => Add($"Cannot coerce variable {symbol.Name} from {symbol.Type} to {targetType.CanonicalName} at {span}");
}