using Byron.Compiler.Lexer;

// ReSharper disable once CheckNamespace
namespace Byron.Compiler.AST.HighLevel;

public record ProgramNode(List<TopLevelDeclarationNode> Declarations);

public abstract record AstNode(SourceSpan Span);

public abstract record TopLevelDeclarationNode(SourceSpan Span) : AstNode(Span);

public record ParameterNode(
    ReceiverBindingOwnership Ownership,
    string Name, 
    TypeNode Type, 
    SourceSpan Span) : AstNode(Span);

public record FunctionDeclarationNode(
    string Name, 
    List<ParameterNode> Parameters, 
    TypeNode ReturnType, 
    BlockStatementNode Body, 
    SourceSpan Span
) : TopLevelDeclarationNode(Span);

public abstract record StatementNode(SourceSpan Span) : AstNode(Span);
public record BlockStatementNode(List<StatementNode> Statements, SourceSpan Span) : StatementNode(Span);
public record ReturnStatementNode(ExpressionNode? Expression, SourceSpan Span) : StatementNode(Span);
public record YieldStatementNode(ExpressionNode Expression, SourceSpan Span) : StatementNode(Span);
public record DiscardStatementNode(ExpressionNode Initializer, SourceSpan Span) : StatementNode(Span);
public record VariableDeclarationNode(
    bool IsMutable,
    string Name, 
    TypeNode? ExplicitType, 
    ExpressionNode Initializer, 
    SourceSpan Span
) : StatementNode(Span);

public abstract record ExpressionNode(SourceSpan Span) : AstNode(Span);
public record IntegerLiteralNode(long Value, SourceSpan Span) : ExpressionNode(Span);
public record BoolLiteralNode(bool Value, SourceSpan Span) : ExpressionNode(Span);
public record VariableExpressionNode(string Name, SourceSpan Span) : ExpressionNode(Span);
public record CallExpressionNode(ExpressionNode Callee, List<ExpressionNode> Arguments, SourceSpan Span) : ExpressionNode(Span);
public record BinaryExpressionNode(ExpressionNode Left, BinaryOperator Operator, ExpressionNode Right, SourceSpan Span) : ExpressionNode(Span);

public abstract record TypeNode(SourceSpan Span) : AstNode(Span);
public record ReferenceTypeNode(TypeNode Target, bool IsMutable, SourceSpan Span) : TypeNode(Span);

// Built-in types
public abstract record BuiltInTypeNode(SourceSpan Span) : TypeNode(Span);
public record VoidTypeNode(SourceSpan Span) : BuiltInTypeNode(Span);
public record UnitTypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record Int8TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record Int16TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record Int32TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record Int64TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record UInt8TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record UInt16TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record UInt32TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record UInt64TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record Float32TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record Float64TypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record BoolTypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

public record RuneTypeNode(SourceSpan Span) : BuiltInTypeNode(Span);

// Lowerable expressions
public record OnErrorExpressionNode(ExpressionNode Source, ExpressionNode Fallback, SourceSpan Span) : ExpressionNode(Span);
public record TryOperatorExpressionNode(ExpressionNode Source, SourceSpan Span) : ExpressionNode(Span);