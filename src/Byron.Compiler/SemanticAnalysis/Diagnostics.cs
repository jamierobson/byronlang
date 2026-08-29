using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Lexer;

namespace Byron.Compiler.SemanticAnalysis;

public class Diagnostics
{
    public bool HasErrors => _diagnosticMessages.Count > 0;
    
    private readonly List<string> _diagnosticMessages = [];
    public IReadOnlyList<string> DiagnosticMessages => _diagnosticMessages;
    
    public void UndeclaredType(TypeNode type, string? usage = null) => 
        Add(string.IsNullOrWhiteSpace(usage)
            ? $"The type {type.Symbol} could not be found at {type.Span}. Are you missing an import or alias?"
            : $"The {usage} type {type.Symbol} could not be found at {type.Span}. Are you missing an import or alias?");
    
            
    
    private void Add(string message) => _diagnosticMessages.Add(message);

    public void UndeclaredAlias(AliasDeclarationNode node) => Add($"The alias {node.Symbol} could not be found at {node.Span}. Are you missing an import?");
    public void Duplicate(ModuleDeclarationNode node, SourceSpan duplicateSpan) => Add($"Duplicate module declaration {node.Symbol} at {node.Span}. Originally declared at {duplicateSpan}");
    public void Duplicate(FunctionDeclarationNode node, SourceSpan duplicateSpan) => Add($"Duplicate function declaration {node.Symbol} at {node.Span}. Originally declared at {duplicateSpan}");
    public void Duplicate(TraitDeclarationNode node, SourceSpan duplicateSpan) => Add($"Duplicate trait declaration {node.Symbol} at {node.Span}. Originally declared at {duplicateSpan}");
    public void Duplicate(StructDeclarationNode node, SourceSpan duplicateSpan) => Add($"Duplicate struct declaration {node.Symbol} at {node.Span}. Originally declared at {duplicateSpan}");
    public void Duplicate(AliasDeclarationNode node, SourceSpan duplicateSpan) => Add($"Duplicate aliasS {node.Symbol} at {node.Span}. Originally declared at {duplicateSpan}");
    public void Duplicate(VariableDeclarationNode variableDeclaration) => Add($"A local variable {variableDeclaration.Name} cannot be declared at {variableDeclaration.Span} because the variable name already has another binding");
    public void TypeMismatch(TypeNode leftType, TypeNode rightType) => Add($"Cannot convert {leftType.Symbol} to type {rightType.Symbol} at {rightType.Span}");
    public void TypeMismatch(TypeNode leftType, string rightType) => Add($"Cannot convert {leftType.Symbol} to type {rightType} at {leftType.Span}");
    public void OutOfRange(IntegerLiteralNode literal, TypeNode type) => Add($"{literal.Value} is out of range for type {type.Symbol} at {literal.Span}");
    public void MissingMember(string canonicalName, MemberAccessExpressionNode expression) => Add($"{canonicalName} does not contain field {expression.MemberName} at {expression.Span}");
    public void MissingMember(AST.Symbol symbol, MemberAccessExpressionNode expression) => Add($"{symbol} does not contain field {expression.MemberName} at {expression.Span}");
    public void MissingMember(string canonicalName, StructFieldInitializerNode initializer) => Add($"{canonicalName} does not contain field {initializer.FieldName} at {initializer.Span}");
    public void MissingMember(AST.Symbol symbol, StructFieldInitializerNode initializer) => Add($"{symbol} does not contain field {initializer.FieldName} at {initializer.Span}");
    public void InvalidStructName(StructDeclarationNode structDeclaration, string canonicalName) => Add($"Invalid struct name {structDeclaration.Symbol} at {structDeclaration.Span}");
    // public void CircularReference(Symbol symbol, SourceSpan sourceSpan) => Add($"Circular reference in type {symbol} at {sourceSpan}");
    // public void CircularReference(AliasDeclarationNode alias) => Add($"Circular reference in type {alias.Name} at {alias.Span}");
    public void CircularReference(AliasDeclarationNode alias, HashSet<string> cycle) => Add($"Circular reference in type {alias.Name} at {alias.Span}. The cycle contains the aliases {string.Join(',', cycle)}");
    public void UndeclaredVariable(VariableExpressionNode variableExpression) => Add($"Cannot resolve symbol {variableExpression.Name} at {variableExpression.Span}");
    public void UndeclaredTrait(TraitTypeNode type) => Add($"The trait {type.Symbol} could not be found at {type.Span}. Are you missing an import or alias?");
    public void UndeclaredFunction(string functionName, SourceSpan sourceSpan) => Add($"Cannot resolve function {functionName} at {sourceSpan}");
    public void InvalidArgument(AST.Symbol argumentType, AST.Symbol parameterType, Symbol function, SourceSpan span) => Add($"Argument type {argumentType} is not assignable to parameter type {parameterType} in function {function}at {span}");
    public void InvalidMutation(VariableExpressionNode variable, SourceSpan typeSpan) => Add($"Variable {variable.Name} is is mutated at {variable.Span} but declared immutable at {typeSpan}");
    public void InvalidUnaryOperation(UnaryExpressionNode unary, TypeNode operandType) => Add($"Cannot apply {unary.Operator.ToLexeme()} operator to type {operandType.Symbol} at {unary.Span}");
    public void InvalidDereference(DereferenceExpressionNode dereference, TypeNode targetType) => Add($"Cannot dereference a non-reference type {targetType.Symbol} at {dereference.Span}");
    public void InvalidArgumentCount(CallExpressionNode callExpression, FunctionDeclarationNode function) => Add($"{function.Symbol} has {function.Signature.Parameters.Count} parameter(s) but is invoked with {callExpression.Arguments.Count} arguments at {callExpression.Span}");
    public void NoSelfArgument(FunctionDeclarationNode function, SourceSpan callSiteSpan) => Add($"The function {function.Symbol} does not expose a first argument self at {callSiteSpan}");
    public void InvalidSelfArgumentOutsideOfImplementBlock(FunctionSignatureNode function, SourceSpan callSiteSpan) => Add($"Cannot bind the Self type for function {function.Name}. Functions referring to self must be placed inside an implement block or a trait definition at {callSiteSpan}");
    public void InvalidSelfArgumentPosition(FunctionDeclarationNode function, ParameterNode parameter) => Add($"{parameter.Name} must be the first parameter declared in the function {function.Symbol} at {function.Span}"); 
    public void InvalidSelfArgumentType(string parameterType, string expectedType, FunctionDeclarationNode function) => Add($"The self parameter of function {function.Symbol} should be of type {expectedType}, but was declared as {parameterType} at {function.Span}");
    public void InvalidCast(LookupSymbol lookupSymbol, TypeNode targetType, SourceSpan span) => Add($"Cannot coerce variable {lookupSymbol.Name} from {lookupSymbol.Type} to {targetType.Symbol} at {span}");
    public void AmbiguousEntryPoint(FunctionDeclarationNode function, SourceSpan functionSpan) => Add($"Ambiguous application entry point: duplicate definition of {FunctionSignatureNode.EntryFunctionName} at  {function.Span}.");
    public void InvalidStructInitializationType(NominalTypeNode initializationNominalType, SourceSpan initializationSpan) => Add($"Invalid struct initialization: {initializationNominalType} is not a valid struct type at {initializationSpan}");
    public void MissingTraitImplementationField(ImplementBlockDeclarationNode block, string requiredFieldName) => Add($"{block.TypeNode.Symbol} does not implement field {requiredFieldName} required by trait {block.TraitNode!.Symbol}. The implementation is declared at {block.Span}");
    public void MissingTraitImplementationFunction(ImplementBlockDeclarationNode block, string requiredFieldName) => Add($"{block.TypeNode.Symbol} does not implement function {requiredFieldName} required by trait {block.TraitNode!.Symbol}. The implementation is declared at {block.Span}");

    public void InvalidTraitImplementationFunctionSignature(ImplementBlockDeclarationNode block,
        FunctionSignatureNode requiredFunction, FunctionSignatureNode declaredFunction) => Add(
        $"{block.TypeNode.Symbol} does not implement function {declaredFunction.Name} exactly as required for implementation of trait {block.TraitNode
            .Symbol}. Expected {requiredFunction.SignatureString()}, but found {declaredFunction.SignatureString()}. The implementation is declared at {block.Span}");
}