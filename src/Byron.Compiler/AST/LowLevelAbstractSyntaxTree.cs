// ReSharper disable once CheckNamespace
namespace Byron.Compiler.AST.LowLevel;

public record ProgramNode(List<TopLevelDeclarationNode> Declarations);

public abstract record AstNode;

// Top level declarations
public abstract record TopLevelDeclarationNode : AstNode;

// Functions
public record FunctionDeclarationNode(string Name, List<ParameterNode> Parameters, TypeNode ReturnType, BlockStatementNode Body ) : TopLevelDeclarationNode;
public record ParameterNode(ReceiverBindingOwnership Ownership, string Name, TypeNode Type) : AstNode;

// Structs
public record StructDeclarationNode(string Name, List<StructFieldNode> Fields) : TopLevelDeclarationNode;
public record StructFieldNode(string Name, TypeNode Type) : AstNode;
public record StructFieldInitializerNode(string FieldName, ExpressionNode Value) : AstNode;


// Statements
public abstract record StatementNode : AstNode;
public record BlockStatementNode(List<StatementNode> Statements) : StatementNode;
public record ReturnStatementNode(ExpressionNode? Expression) : StatementNode;
public record YieldStatementNode(ExpressionNode Expression) : StatementNode;
public record DiscardStatementNode(ExpressionNode Initializer) : StatementNode;
public record VariableDeclarationNode(bool IsMutable, string Name, TypeNode? ExplicitType, ExpressionNode Initializer) : StatementNode;
public record AssignmentStatementNode(ExpressionNode Target, ExpressionNode Value) : StatementNode;
public record ExpressionStatementNode(ExpressionNode Expression) : StatementNode;

public record IfStatementNode(ExpressionNode Condition, BlockStatementNode ThenBranch) : StatementNode;
public record IfElseStatementNode(ExpressionNode Condition, BlockStatementNode ThenBranch, BlockStatementNode ElseBranch ) : IfStatementNode(Condition, ThenBranch);

public record WhileStatement(ExpressionNode ContinuationCondition, BlockStatementNode Body ): StatementNode;
public record BreakStatement: StatementNode;
public record ContinueStatement: StatementNode;

// Expressions
public abstract record ExpressionNode : AstNode;
public record IntegerLiteralNode(long Value) : ExpressionNode;
public record BoolLiteralNode(bool Value) : ExpressionNode;
public record VariableExpressionNode(string Name) : ExpressionNode;
public record CallExpressionNode(ExpressionNode Callee, List<ExpressionNode> Arguments) : ExpressionNode;
public record BinaryExpressionNode(ExpressionNode Left, BinaryOperator Operator, ExpressionNode Right) : ExpressionNode;

public record StructFieldInitializationExpressionNode(string StructName, List<StructFieldInitializerNode> FieldInitializers) : ExpressionNode;
public record MemberAccessExpressionNode(ExpressionNode Target, string MemberName) : ExpressionNode;

// public record BlockExpressionNode(List<StatementNode> Statements) : ExpressionNode;
// public record MatchExpressionNode(ReceiverBindingOwnership BindingOwnership, ExpressionNode Source, List<MatchExpressionArmNode> Arms) : ExpressionNode;
// public record MatchExpressionArmNode(bool IsMutable, string VariantName, string BindingIdentifier, BlockExpressionNode Body) : AstNode;

// Types
public abstract record TypeNode : AstNode { public abstract string ToLlvmTypeString(); }
public record UserDeclaredTypeNode(string FullyQualifiedName) : TypeNode { public override string ToLlvmTypeString() => $"%{FullyQualifiedName}"; };

public record ReferenceTypeNode(TypeNode Target, bool IsMutable) : TypeNode { public override string ToLlvmTypeString() => $"{Target.ToLlvmTypeString()}*"; }

public abstract record BuiltInTypeNode : TypeNode;
public record Int8TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i8"; }
public record Int16TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i16"; }
public record Int32TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i32"; }
public record Int64TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i64"; }

public record UInt8TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i8"; }
public record UInt16TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i16"; }
public record UInt32TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i32"; }
public record UInt64TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i64"; }

public record Float32TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "float"; }
public record Float64TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "double"; }

public record BoolTypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i1"; }
public record RuneTypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i32"; }
public record VoidTypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "void"; }
public record UnitTypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "void"; }