using High = Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.AST.LowLevel;

public abstract record AstNode(High.AstNode SourceNode);

public record ProgramNode(List<TopLevelDeclarationNode> Declarations);

// Top level declarations
public abstract record TopLevelDeclarationNode(High.AstNode SourceNode, string CanonicalName) : AstNode(SourceNode);

// Functions
public record FunctionDeclarationNode(
    High.AstNode SourceNode,
    FunctionSignatureNode Signature,
    BlockStatementNode Body) : TopLevelDeclarationNode(SourceNode, Signature.CanonicalName);

public record FunctionSignatureNode(
    High.AstNode SourceNode,
    string CanonicalName,
    List<ParameterNode> Parameters,
    TypeNode ReturnType) : AstNode(SourceNode);

public record ParameterNode(High.AstNode SourceNode, string Name, ReceiverBindingOwnership Ownership, TypeNode Type)
    : AstNode(SourceNode);

// Structs
public record StructDeclarationNode(High.AstNode SourceNode, string CanonicalName, List<StructFieldNode> Fields)
    : TopLevelDeclarationNode(SourceNode, CanonicalName);

public record StructFieldNode(High.AstNode SourceNode, string Name, TypeNode Type) : AstNode(SourceNode);

public record StructFieldInitializerNode(High.AstNode SourceNode, string FieldName, ExpressionNode Value)
    : AstNode(SourceNode);

// Statements
public abstract record StatementNode(High.AstNode SourceNode) : AstNode(SourceNode);
public record BlockStatementNode(High.AstNode SourceNode, List<StatementNode> Statements) : StatementNode(SourceNode);

public record ReturnStatementNode(High.AstNode SourceNode, ExpressionNode? Expression)
    : StatementNode(SourceNode);

public record YieldStatementNode(High.AstNode SourceNode, ExpressionNode Expression) : StatementNode(SourceNode);

public record DiscardStatementNode(High.AstNode SourceNode, ExpressionNode Initializer) : StatementNode(SourceNode);

public record VariableDeclarationNode(
    High.AstNode SourceNode,
    string Name,
    bool IsMutable,
    TypeNode? ExplicitType,
    ExpressionNode Initializer) : StatementNode(SourceNode);

public record AssignmentStatementNode(
    High.AstNode SourceNode,
    ExpressionNode Target,
    ExpressionNode Value) : StatementNode(SourceNode);

public record ExpressionStatementNode(High.AstNode SourceNode, ExpressionNode Expression) : StatementNode(SourceNode);

public record IfStatementNode(High.AstNode SourceNode, ExpressionNode Condition, BlockStatementNode ThenBranch)
    : StatementNode(SourceNode);
public record IfElseStatementNode(High.AstNode SourceNode, ExpressionNode Condition, BlockStatementNode ThenBranch, BlockStatementNode ElseBranch ) : IfStatementNode(SourceNode, Condition, ThenBranch);

public record WhileStatement(High.AstNode SourceNode, ExpressionNode ContinuationCondition, BlockStatementNode Body)
    : StatementNode(SourceNode);
public record BreakStatement(High.BreakStatement SourceNode): StatementNode(SourceNode)
{
    public new High.BreakStatement SourceNode => (High.BreakStatement)base.SourceNode;
}

public record ContinueStatement(High.AstNode SourceNode) : StatementNode(SourceNode);
// Expressions
public abstract record ExpressionNode(High.AstNode SourceNode) : AstNode(SourceNode);

public record IntegerLiteralNode(High.AstNode SourceNode, long Value) : ExpressionNode(SourceNode);

public record FloatLiteralNode(High.AstNode SourceNode, double Value) : ExpressionNode(SourceNode);

public record BoolLiteralNode(High.AstNode SourceNode, bool Value) : ExpressionNode(SourceNode);

public record VariableExpressionNode(High.AstNode SourceNode, string Name) : ExpressionNode(SourceNode);

public record CallExpressionNode(High.AstNode SourceNode, ExpressionNode Callee, List<ExpressionNode> Arguments)
    : ExpressionNode(SourceNode);

public record BinaryExpressionNode(
    High.AstNode SourceNode,
    ExpressionNode Left,
    BinaryOperator Operator,
    ExpressionNode Right) : ExpressionNode(SourceNode);

public record StructFieldInitializationExpressionNode(
    High.AstNode SourceNode,
    NominalTypeNode Type,
    List<StructFieldInitializerNode> FieldInitializers) : ExpressionNode(SourceNode);

public record MemberAccessExpressionNode(
    High.AstNode SourceNode,
    ExpressionNode Target,
    string MemberName) : ExpressionNode(SourceNode);

public record DereferenceExpressionNode(High.AstNode SourceNode, ExpressionNode Target, TypeNode ExpressionType) : ExpressionNode(SourceNode);
public record AddressOfExpressionNode(High.AstNode SourceNode, ExpressionNode Target) : ExpressionNode(SourceNode);
public record ExtendIntegerNode(High.AstNode SourceNode, ExpressionNode Operand, IntegerTypeNode TargetType)
    : ExpressionNode(SourceNode);

public record ExtendFloatNode(High.AstNode SourceNode, ExpressionNode Operand, FloatTypeNode TargetType)
    : ExpressionNode(SourceNode);

public record CastIntToFloatNode(
    High.AstNode SourceNode,
    ExpressionNode Operand,
    FloatTypeNode TargetType,
    bool SourceTypeIsSigned) : ExpressionNode(SourceNode);

public record CastFloatToIntNode(High.AstNode SourceNode, ExpressionNode Operand, IntegerTypeNode TargetType)
    : ExpressionNode(SourceNode);

// public record BlockExpressionNode(List<StatementNode> Statements) : ExpressionNode;
// public record MatchExpressionNode(ReceiverBindingOwnership BindingOwnership, ExpressionNode Source, List<MatchExpressionArmNode> Arms) : ExpressionNode;
// public record MatchExpressionArmNode(bool IsMutable, string VariantName, string BindingIdentifier, BlockExpressionNode Body) : AstNode;

// Types
public abstract record TypeNode(High.AstNode SourceNode) : AstNode(SourceNode);
public record NominalTypeNode(High.AstNode SourceNode, string CanonicalName) : TypeNode(SourceNode);
public record ReferenceTypeNode(High.AstNode SourceNode, TypeNode Target) : TypeNode(SourceNode);
public abstract record BuiltInTypeNode(High.AstNode SourceNode) : TypeNode(SourceNode);
public abstract record PrimitiveTypeNode(High.AstNode SourceNode) : BuiltInTypeNode(SourceNode);
public record IntegerTypeNode(High.AstNode SourceNode, int BitWidth, bool Signed) : PrimitiveTypeNode(SourceNode);
public record UnsignedIntTypeNode(High.AstNode SourceNode, int BitWidth, bool Signed) : IntegerTypeNode(SourceNode, BitWidth, Signed);
public record SignedIntTypeNode(High.AstNode SourceNode, int BitWidth, bool Signed) : IntegerTypeNode(SourceNode, BitWidth, Signed);
public record FloatTypeNode(High.AstNode SourceNode, int BitWidth) : PrimitiveTypeNode(SourceNode);
public record BoolTypeNode(High.AstNode SourceNode) : PrimitiveTypeNode(SourceNode);
public record RuneTypeNode(High.AstNode SourceNode) : PrimitiveTypeNode(SourceNode);
public record VoidTypeNode(High.AstNode SourceNode) : PrimitiveTypeNode(SourceNode);