using Byron.Compiler.Lexer;

// ReSharper disable once CheckNamespace
namespace Byron.Compiler.AST.LowLevel;

public record ProgramNode(List<TopLevelDeclarationNode> Declarations);

public abstract record AstNode;

public abstract record TopLevelDeclarationNode : AstNode;

public record ParameterNode(
    ReceiverBindingOwnership Ownership,
    string Name, 
    TypeNode Type
    ) : AstNode;

public record FunctionDeclarationNode(
    string Name, 
    List<ParameterNode> Parameters, 
    TypeNode ReturnType, 
    BlockStatementNode Body
) : AstNode;

public abstract record StatementNode : AstNode;
public record BlockStatementNode(List<StatementNode> Statements) : StatementNode;
public record ReturnStatementNode(ExpressionNode? Expression) : StatementNode;
public record YieldStatementNode(ExpressionNode Expression) : StatementNode;
public record DiscardStatementNode(ExpressionNode Initializer) : StatementNode;
public record VariableDeclarationNode(
    bool IsMutable, 
    string Name, 
    TypeNode? ExplicitType, 
    ExpressionNode Initializer) : StatementNode;

public abstract record ExpressionNode : AstNode;
public record IntegerLiteralNode(long Value) : ExpressionNode;
public record VariableExpressionNode(string Name) : ExpressionNode;
public record CallExpressionNode(ExpressionNode Callee, List<ExpressionNode> Arguments) : ExpressionNode;
public record BinaryExpressionNode(ExpressionNode Left, BinaryOperator Operator, ExpressionNode Right) : ExpressionNode;

// public record BlockExpressionNode(List<StatementNode> Statements) : ExpressionNode;
// public record MatchExpressionNode(ReceiverBindingOwnership BindingOwnership, ExpressionNode Source, List<MatchExpressionArmNode> Arms) : ExpressionNode;
// public record MatchExpressionArmNode(bool IsMutable, string VariantName, string BindingIdentifier, BlockExpressionNode Body) : AstNode;

// Low-Level Types carrying LLVM conversion logic directly
public abstract record TypeNode : AstNode { public abstract string ToLlvmTypeString(); }
public abstract record BuiltInTypeNode : TypeNode;
public record VoidTypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "void"; }
public record UnitTypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "void"; }
public record Int64TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i64"; }
public record Int32TypeNode : BuiltInTypeNode { public override string ToLlvmTypeString() => "i32"; }
public record ReferenceTypeNode(TypeNode Target, bool IsMutable) : TypeNode { public override string ToLlvmTypeString() => $"{Target.ToLlvmTypeString()}*"; }