using High = Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.AST.LowLevel;

public record ProgramNode(List<TopLevelDeclarationNode> Declarations);

public abstract record AstNode(High.AstNode SourceNode);

// Top level declarations
public abstract record TopLevelDeclarationNode(High.TopLevelDeclarationNode SourceNode) : AstNode(SourceNode)
{
    public new High.TopLevelDeclarationNode SourceNode => (High.TopLevelDeclarationNode)base.SourceNode;
}

// Functions
public record FunctionDeclarationNode(High.FunctionDeclarationNode SourceNode, FunctionSignatureNode Signature, BlockStatementNode Body) : TopLevelDeclarationNode(SourceNode)
{
    public new High.FunctionDeclarationNode SourceNode => (High.FunctionDeclarationNode)base.SourceNode;
    public string Name => SourceNode.Name;
}

public record FunctionSignatureNode(High.FunctionSignatureNode SourceNode, List<ParameterNode> Parameters, TypeNode ReturnType) : AstNode(SourceNode)
{
    public new High.FunctionSignatureNode SourceNode => (High.FunctionSignatureNode)base.SourceNode;
}

public record ParameterNode(High.ParameterNode SourceNode, TypeNode Type) : AstNode(SourceNode)
{
    public new High.ParameterNode SourceNode => (High.ParameterNode)base.SourceNode;
    public string Name => SourceNode.Name;
    public ReceiverBindingOwnership Ownership => SourceNode.Ownership;
}

// Traits
public record TraitDeclarationNode(High.TraitDeclarationNode SourceNode, List<StructFieldNode> RequiredFields, List<FunctionSignatureNode> RequiredFunctions) : TopLevelDeclarationNode(SourceNode)
{
    public new High.TraitDeclarationNode SourceNode => (High.TraitDeclarationNode)base.SourceNode;
    public string Name => SourceNode.Name;
}

// Structs
public record StructDeclarationNode(High.StructDeclarationNode SourceNode, List<StructFieldNode> Fields) : TopLevelDeclarationNode(SourceNode)
{
    public new High.StructDeclarationNode SourceNode => (High.StructDeclarationNode)base.SourceNode;
    public string Name => SourceNode.Name;
}

public record StructFieldNode(High.StructFieldNode SourceNode, TypeNode Type) : AstNode(SourceNode)
{
    public new High.StructFieldNode SourceNode => (High.StructFieldNode)base.SourceNode;
    public string Name => SourceNode.Name;
}

public record StructFieldInitializerNode(High.StructFieldInitializerNode SourceNode, ExpressionNode Value) : AstNode(SourceNode)
{
    public new High.StructFieldInitializerNode SourceNode => (High.StructFieldInitializerNode)base.SourceNode;
    
    public string FieldName => SourceNode.FieldName;
}

// Statements
public abstract record StatementNode(High.StatementNode SourceNode) : AstNode(SourceNode)
{
    public new High.StatementNode SourceNode => (High.StatementNode)base.SourceNode;
}

public record BlockStatementNode(High.BlockStatementNode SourceNode, List<StatementNode> Statements) : StatementNode(SourceNode)
{
    public new High.BlockStatementNode SourceNode => (High.BlockStatementNode)base.SourceNode;
}

public record ReturnStatementNode(High.ReturnStatementNode SourceNode, ExpressionNode? Expression) : StatementNode(SourceNode)
{
    public new High.ReturnStatementNode SourceNode => (High.ReturnStatementNode)base.SourceNode;
}

public record YieldStatementNode(High.YieldStatementNode SourceNode, ExpressionNode Expression) : StatementNode(SourceNode)
{
    public new High.YieldStatementNode SourceNode => (High.YieldStatementNode)base.SourceNode;
}

public record DiscardStatementNode(High.DiscardStatementNode SourceNode, ExpressionNode Initializer) : StatementNode(SourceNode)
{
    public new High.DiscardStatementNode SourceNode => (High.DiscardStatementNode)base.SourceNode;
}

public record VariableDeclarationNode(High.VariableDeclarationNode SourceNode, TypeNode? ExplicitType, ExpressionNode Initializer) : StatementNode(SourceNode)
{
    public new High.VariableDeclarationNode SourceNode => (High.VariableDeclarationNode)base.SourceNode;
    
    public string Name => SourceNode.Name;
    public bool IsMutable => SourceNode.IsMutable;
}

public record AssignmentStatementNode(High.AssignmentStatementNode SourceNode, ExpressionNode Target, ExpressionNode Value) : StatementNode(SourceNode)
{
public new High.AssignmentStatementNode SourceNode => (High.AssignmentStatementNode)base.SourceNode;
}

public record ExpressionStatementNode(High.ExpressionStatementNode SourceNode, ExpressionNode Expression) : StatementNode(SourceNode)
{
    public new High.ExpressionStatementNode SourceNode => (High.ExpressionStatementNode)base.SourceNode;
}

public record IfStatementNode(High.IfElseStatement SourceNode, ExpressionNode Condition, BlockStatementNode ThenBranch) : StatementNode(SourceNode)
{
    public new High.IfElseStatement SourceNode => (High.IfElseStatement)base.SourceNode;
}

public record IfElseStatementNode(High.IfElseStatement SourceNode, ExpressionNode Condition, BlockStatementNode ThenBranch, BlockStatementNode ElseBranch ) : IfStatementNode(SourceNode, Condition, ThenBranch);

public record WhileStatement(High.WhileStatement SourceNode, ExpressionNode ContinuationCondition, BlockStatementNode Body ): StatementNode(SourceNode)
{
    public new High.WhileStatement SourceNode => (High.WhileStatement)base.SourceNode;
}

public record BreakStatement(High.BreakStatement SourceNode): StatementNode(SourceNode)
{
    public new High.BreakStatement SourceNode => (High.BreakStatement)base.SourceNode;
}

public record ContinueStatement(High.ContinueStatement SourceNode): StatementNode(SourceNode)
{
    public new High.ContinueStatement SourceNode => (High.ContinueStatement)base.SourceNode;
}

// Expressions
public abstract record ExpressionNode(High.ExpressionNode SourceNode) : AstNode(SourceNode)
{
    public new High.ExpressionNode SourceNode => (High.ExpressionNode)base.SourceNode;
}

public record IntegerLiteralNode(High.IntegerLiteralNode SourceNode) : ExpressionNode(SourceNode)
{
    public new High.IntegerLiteralNode SourceNode => (High.IntegerLiteralNode)base.SourceNode;
    public long Value => SourceNode.Value;
}

public record FloatLiteralNode(High.FloatLiteralNode SourceNode) : ExpressionNode(SourceNode)
{
    public new High.FloatLiteralNode SourceNode => (High.FloatLiteralNode)base.SourceNode;
    public double Value => SourceNode.Value;
}

public record BoolLiteralNode(High.BooleanLiteralNode SourceNode) : ExpressionNode(SourceNode)
{
    public new High.BooleanLiteralNode SourceNode => (High.BooleanLiteralNode)base.SourceNode;
    public bool Value => SourceNode.Value;
}

public record VariableExpressionNode(High.VariableExpressionNode SourceNode) : ExpressionNode(SourceNode)
{
    public new High.VariableExpressionNode SourceNode => (High.VariableExpressionNode)base.SourceNode;
    public  string Name => SourceNode.Name;
}

public record CallExpressionNode(High.CallExpressionNode SourceNode, ExpressionNode Callee, List<ExpressionNode> Arguments) : ExpressionNode(SourceNode)
{
    public new High.CallExpressionNode SourceNode => (High.CallExpressionNode)base.SourceNode;
}

public record BinaryExpressionNode(High.BinaryExpressionNode SourceNode, ExpressionNode Left, ExpressionNode Right) : ExpressionNode(SourceNode)
{
    public new High.BinaryExpressionNode SourceNode => (High.BinaryExpressionNode)base.SourceNode;
    public BinaryOperator Operator =>  SourceNode.Operator; 
}

public record StructFieldInitializationExpressionNode(High.StructFieldInitializationExpressionNode SourceNode, List<StructFieldInitializerNode> FieldInitializers) : ExpressionNode(SourceNode)
{
    public new High.StructFieldInitializationExpressionNode SourceNode => (High.StructFieldInitializationExpressionNode)base.SourceNode;
    public string StructName => SourceNode.NominalType.Name;
}

public record MemberAccessExpressionNode(High.MemberAccessExpressionNode SourceNode, ExpressionNode Target, string MemberName) : ExpressionNode(SourceNode)
{
    public new High.MemberAccessExpressionNode SourceNode => (High.MemberAccessExpressionNode)base.SourceNode;
}

public record DereferenceExpressionNode(High.DereferenceExpressionNode SourceNode, ExpressionNode Target) : ExpressionNode(SourceNode)
{
    public new High.DereferenceExpressionNode SourceNode => (High.DereferenceExpressionNode)base.SourceNode;
}

public record AddressOfExpressionNode(High.AddressOfExpressionNode SourceNode, ExpressionNode Target) : ExpressionNode(SourceNode)
{
    public new High.AddressOfExpressionNode SourceNode => (High.AddressOfExpressionNode)base.SourceNode;
}

public record ExtendIntegerNode(High.ExtendIntegerNode SourceNode, ExpressionNode Operand, IntegerTypeNode TargetType) : ExpressionNode(SourceNode)
{
    public new High.ExtendIntegerNode SourceNode => (High.ExtendIntegerNode)base.SourceNode;
}
public record ExtendFloatNode(High.ExtendFloatNode SourceNode, ExpressionNode Operand, FloatTypeNode TargetType) : ExpressionNode(SourceNode)
{
    public new High.ExtendFloatNode SourceNode => (High.ExtendFloatNode)base.SourceNode;
}

public record CastIntToFloatNode(High.CastIntToFloatNode SourceNode, ExpressionNode Operand, FloatTypeNode TargetType) : ExpressionNode(SourceNode)
{
    public new High.CastIntToFloatNode SourceNode => (High.CastIntToFloatNode)base.SourceNode;
    public bool SourceTypeIsSigned =>  SourceNode.SourceTypeIsSigned;
}

public record CastFloatToIntNode(High.CastFloatToIntNode SourceNode, ExpressionNode Operand, IntegerTypeNode TargetType) : ExpressionNode(SourceNode)
{
    public new High.CastFloatToIntNode SourceNode => (High.CastFloatToIntNode)base.SourceNode;
}

// public record BlockExpressionNode(List<StatementNode> Statements) : ExpressionNode;
// public record MatchExpressionNode(ReceiverBindingOwnership BindingOwnership, ExpressionNode Source, List<MatchExpressionArmNode> Arms) : ExpressionNode;
// public record MatchExpressionArmNode(bool IsMutable, string VariantName, string BindingIdentifier, BlockExpressionNode Body) : AstNode;

// Types
public abstract record TypeNode(High.TypeNode SourceNode) : AstNode(SourceNode)
{
    public new High.TypeNode SourceNode => (High.TypeNode)base.SourceNode;
}

public record NominalTypeNode(High.NominalTypeNode SourceNode) : TypeNode(SourceNode)
{
    public new High.NominalTypeNode SourceNode => (High.NominalTypeNode)base.SourceNode;
    public string CanonicalName => SourceNode.CanonicalName.ToString(); // todo: Should this also be a CanonicalName type
}

public record ReferenceTypeNode(High.TypeNode SourceNode, TypeNode Target) : TypeNode(SourceNode);

public abstract record BuiltInTypeNode(High.BuiltInTypeNode SourceNode) : TypeNode(SourceNode)
{
    public new High.BuiltInTypeNode SourceNode => (High.BuiltInTypeNode)base.SourceNode;
}

public abstract record PrimitiveTypeNode(High.PrimitiveTypeNode SourceNode) : BuiltInTypeNode(SourceNode)
{
    public new High.PrimitiveTypeNode SourceNode => (High.PrimitiveTypeNode)base.SourceNode;
}

public record IntegerTypeNode(High.IntegerTypeNode SourceNode) : PrimitiveTypeNode(SourceNode)
{
    public new High.IntegerTypeNode SourceNode => (High.IntegerTypeNode)base.SourceNode;
    public int BitWidth => SourceNode.BitWidth;
    public bool Signed => SourceNode.Signed;
}

public record UnsignedIntTypeNode(High.UnsignedIntTypeNode SourceNode) : IntegerTypeNode(SourceNode)
{
    public new High.UnsignedIntTypeNode SourceNode => (High.UnsignedIntTypeNode)base.SourceNode;
}

public record SignedIntTypeNode(High.SignedIntTypeNode SourceNode) : IntegerTypeNode(SourceNode)
{
    public new High.SignedIntTypeNode SourceNode => (High.SignedIntTypeNode)base.SourceNode;
}


public record FloatTypeNode(High.FloatTypeNode SourceNode) : PrimitiveTypeNode(SourceNode)
{
    public new High.FloatTypeNode SourceNode => (High.FloatTypeNode)base.SourceNode;
    public int BitWidth => SourceNode.BitWidth;
}


public record BoolTypeNode(High.BoolTypeNode SourceNode) : PrimitiveTypeNode(SourceNode)
{
    public new High.BoolTypeNode SourceNode => (High.BoolTypeNode)base.SourceNode;
}


public record RuneTypeNode(High.RuneTypeNode SourceNode) : PrimitiveTypeNode(SourceNode)
{
    public new High.RuneTypeNode SourceNode => (High.RuneTypeNode)base.SourceNode;
}

public record VoidTypeNode(High.VoidTypeNode SourceNode) : PrimitiveTypeNode(SourceNode)
{
    public new High.VoidTypeNode SourceNode => (High.VoidTypeNode)base.SourceNode;
}
