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
public record FloatLiteralNode(double Value) : ExpressionNode;
public record BoolLiteralNode(bool Value) : ExpressionNode;
public record VariableExpressionNode(string Name) : ExpressionNode;
public record CallExpressionNode(ExpressionNode Callee, List<ExpressionNode> Arguments) : ExpressionNode;
public record BinaryExpressionNode(ExpressionNode Left, BinaryOperator Operator, ExpressionNode Right) : ExpressionNode;

public record StructFieldInitializationExpressionNode(string StructName, List<StructFieldInitializerNode> FieldInitializers) : ExpressionNode;
public record MemberAccessExpressionNode(ExpressionNode Target, string MemberName) : ExpressionNode;

// Casts
// public abstract record CastExpressionNode(ExpressionNode Operand, TypeNode TargetType) : ExpressionNode;
// public record ExtendIntegerNode(ExpressionNode Operand, TypeNode TargetType) : CastExpressionNode(Operand, TargetType);
// public record ExtendFloatNode(ExpressionNode Operand, TypeNode TargetType) : CastExpressionNode(Operand, TargetType);
// public record CastIntToFloatNode(ExpressionNode Operand, TypeNode TargetType, bool Signed) : CastExpressionNode(Operand, TargetType);
// public record CastFloatToIntNode(ExpressionNode Operand, TypeNode TargetType, bool Signed) : CastExpressionNode(Operand, TargetType);
public record ExtendIntegerNode(ExpressionNode Operand, IntegerTypeNode TargetType) : ExpressionNode;
public record ExtendFloatNode(ExpressionNode Operand, FloatTypeNode TargetType) : ExpressionNode;
public record CastIntToFloatNode(ExpressionNode Operand, bool SourceTypeIsSigned, FloatTypeNode TargetType) : ExpressionNode;
public record CastFloatToIntNode(ExpressionNode Operand, IntegerTypeNode TargetType) : ExpressionNode;

// public record BlockExpressionNode(List<StatementNode> Statements) : ExpressionNode;
// public record MatchExpressionNode(ReceiverBindingOwnership BindingOwnership, ExpressionNode Source, List<MatchExpressionArmNode> Arms) : ExpressionNode;
// public record MatchExpressionArmNode(bool IsMutable, string VariantName, string BindingIdentifier, BlockExpressionNode Body) : AstNode;

// Types
public abstract record TypeNode : AstNode;

public record NominalTypeNode(string CanonicalName) : TypeNode;

public record ReferenceTypeNode(TypeNode Target, bool IsMutable) : TypeNode;


public abstract record BuiltInTypeNode : TypeNode;
public abstract record PrimitiveTypeNode : BuiltInTypeNode;

public record IntegerTypeNode(int BitWidth, bool Signed) : PrimitiveTypeNode;
public record UnsignedIntTypeNode(int BitWidth) : IntegerTypeNode(BitWidth, false);
public record SignedIntTypeNode(int BitWidth) : IntegerTypeNode(BitWidth, true);
public record FloatTypeNode(int BitWidth) : PrimitiveTypeNode;

// public record Int8TypeNode() : IntTypeNode(8);
//
// public record Int16TypeNode() : IntTypeNode(16);
//
// public record Int32TypeNode() : IntTypeNode(32);
//
// public record Int64TypeNode() : IntTypeNode(64);
//
// public record UInt8TypeNode() : UIntTypeNode(8);
//
// public record UInt16TypeNode() : UIntTypeNode(16);
//
// public record UInt32TypeNode() : UIntTypeNode(32);
//
// public record UInt64TypeNode() : UIntTypeNode(64);
//
// public record Float32TypeNode() : FloatTypeNode(32);
//
// public record Float64TypeNode() : FloatTypeNode(64);

public record BoolTypeNode : PrimitiveTypeNode;

public record RuneTypeNode : PrimitiveTypeNode;

public record VoidTypeNode : PrimitiveTypeNode;