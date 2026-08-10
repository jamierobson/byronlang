using Byron.Compiler.Lexer;

// ReSharper disable once CheckNamespace
namespace Byron.Compiler.AST.HighLevel;

public record ProgramNode(List<TopLevelDeclarationNode> Declarations);

public readonly record struct NodeId(int Value) : IComparable<NodeId>
{
    public override string ToString() => $"#{Value}";
    public int CompareTo(NodeId other) => Value.CompareTo(other.Value);
}

public abstract record AstNode(SourceSpan Span)
{
    private static int _nextId;
    public NodeId Id { get; } = new(Interlocked.Increment(ref _nextId));
};

// Top Level Declarations
public abstract record TopLevelDeclarationNode(string Name, List<string> ModulePath, SourceSpan Span) : AstNode(Span)
{
    private string? _canonicalName;
    public string CanonicalName()
    {
        return _canonicalName ??= CanonicalNames.InModule(ModulePath, Name);
    }
};

public record FunctionDeclarationNode(string Name, List<string> ModulePath, List<ParameterNode> Parameters, TypeNode ReturnType, BlockStatementNode Body, SourceSpan Span) : TopLevelDeclarationNode(Name, ModulePath, Span);
public record ParameterNode(ReceiverBindingOwnership Ownership, string Name, TypeNode Type, SourceSpan Span) : AstNode(Span);

public record StructDeclarationNode(string Name, List<string> ModulePath, List<StructFieldNode> Fields, SourceSpan Span) : TopLevelDeclarationNode(Name, ModulePath, Span);
public record StructFieldNode(string Name, TypeNode Type, SourceSpan Span) : AstNode(Span);
public record StructFieldInitializerNode(string FieldName, ExpressionNode Value, SourceSpan Span) : AstNode(Span);

// Statements
public abstract record StatementNode(SourceSpan Span) : AstNode(Span);
public record BlockStatementNode(List<StatementNode> Statements, SourceSpan Span) : StatementNode(Span);
public record ReturnStatementNode(ExpressionNode? Expression, SourceSpan Span) : StatementNode(Span);
public record YieldStatementNode(ExpressionNode Expression, SourceSpan Span) : StatementNode(Span); // todo: Since yield turns a block statement into a block region, does that make yield an expression?
public record DiscardStatementNode(ExpressionNode Initializer, SourceSpan Span) : StatementNode(Span);
public record VariableDeclarationNode(bool IsMutable, string Name, TypeNode? TypeAnnotation, ExpressionNode Initializer, SourceSpan Span ) : StatementNode(Span);
public record AssignmentStatementNode(ExpressionNode Target, ExpressionNode Value, SourceSpan Span ) : StatementNode(Span);
public record ExpressionStatementNode(ExpressionNode Expression, SourceSpan Span) : StatementNode(Span);
public record IfElseStatement(ExpressionNode Condition, BlockStatementNode ThenBranch, BlockStatementNode? ElseBranch, SourceSpan Span ) : StatementNode(Span);

public record WhileStatement(ExpressionNode ContinuationCondition, BlockStatementNode Body, SourceSpan Span ): StatementNode(Span);
public record BreakStatement(SourceSpan Span): StatementNode(Span);
public record ContinueStatement(SourceSpan Span): StatementNode(Span);

// Expressions
public abstract record ExpressionNode(SourceSpan Span) : AstNode(Span);
public record IntegerLiteralNode(long Value, SourceSpan Span) : ExpressionNode(Span);
public record FloatLiteralNode(double Value, SourceSpan Span) : ExpressionNode(Span);
public record BoolLiteralNode(bool Value, SourceSpan Span) : ExpressionNode(Span);
public record VariableExpressionNode(string Name, SourceSpan Span) : ExpressionNode(Span);
public record CallExpressionNode(ExpressionNode Callee, List<ExpressionNode> Arguments, SourceSpan Span) : ExpressionNode(Span);
// public record BinaryExpressionNode(ExpressionNode Left, BinaryOperator Operator, ExpressionNode Right, SourceSpan Span) : ExpressionNode(Span); // todo: remove mutability again once we are returning from the visitor nodes

public record BinaryExpressionNode : ExpressionNode
{
    public ExpressionNode Left { get; set; }  // 👈 Mutable set!
    public ExpressionNode Right { get; set; } // 👈 Mutable set!
    public BinaryOperator Operator { get; init; }

    public BinaryExpressionNode(ExpressionNode left, BinaryOperator op, ExpressionNode right, SourceSpan span) 
        : base(span)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}

public record StructFieldInitializationExpressionNode(NominalTypeNode NominalType, List<StructFieldInitializerNode> FieldInitializers, SourceSpan Span) : ExpressionNode(Span);
public record MemberAccessExpressionNode(ExpressionNode Target, string MemberName, SourceSpan Span ) : ExpressionNode(Span);
public record UnaryExpressionNode(UnaryOperator Operator, ExpressionNode Operand, SourceSpan Span) : ExpressionNode(Span);

// Casts
public abstract record CastExpressionNode(ExpressionNode Operand, TypeNode TargetType, SourceSpan Span) : ExpressionNode(Span);

public record ExtendIntegerNode(ExpressionNode Operand, IntegerTypeNode TargetType, SourceSpan Span) : CastExpressionNode(Operand, TargetType, Span)
{
    public new IntegerTypeNode TargetType => (IntegerTypeNode)base.TargetType;
}

public record ExtendFloatNode(ExpressionNode Operand, FloatTypeNode TargetType, SourceSpan Span) : CastExpressionNode(Operand, TargetType, Span)
{
    public new FloatTypeNode TargetType => (FloatTypeNode)base.TargetType;
}

public record CastIntToFloatNode(ExpressionNode Operand, FloatTypeNode TargetType, bool SourceTypeIsSigned, SourceSpan Span) : CastExpressionNode(Operand, TargetType, Span)
{
    public new FloatTypeNode TargetType => (FloatTypeNode)base.TargetType;
}

public record CastFloatToIntNode(ExpressionNode Operand, IntegerTypeNode TargetType, bool IsSigned, SourceSpan Span)
    : CastExpressionNode(Operand, TargetType, Span)
{
    public new IntegerTypeNode TargetType => (IntegerTypeNode)base.TargetType;
}


// Types
public abstract record TypeNode(SourceSpan Span) : AstNode(Span)
{
    private string? _canonicalName;
    protected abstract string GetCanonicalName();

    public string CanonicalName()
    {
        return _canonicalName ??= GetCanonicalName();
    }
};

public record NominalTypeNode(string Name, List<string> ModulePath, SourceSpan Span) : TypeNode(Span)
{
    protected  override string GetCanonicalName() => CanonicalNames.InModule(ModulePath, Name);
}

public record ReferenceTypeNode(TypeNode Target, bool IsMutable, SourceSpan Span) : TypeNode(Span)
{
    protected override string GetCanonicalName() => $"&{(IsMutable ? "var " : "")}{Target.CanonicalName()}";
};

public abstract record BuiltInTypeNode(string Name, List<string> ModulePath, SourceSpan Span) : TypeNode(Span)
{
    protected  override string GetCanonicalName() => CanonicalNames.InModule(ModulePath, Name);
};
public abstract record PrimitiveTypeNode(string Name, SourceSpan Span): BuiltInTypeNode(Name, [], Span);

public record VoidTypeNode(SourceSpan Span) : PrimitiveTypeNode(PrimitiveTypeNames.@void, Span);

public abstract record NumericTypeNode(string Name, bool Signed, SourceSpan Span) : PrimitiveTypeNode(Name, Span);
public abstract record IntegerTypeNode(string Name, int BitWidth, bool Signed, SourceSpan Span) : NumericTypeNode(Name, Signed, Span);
public record UnsignedIntTypeNode(string Name, int BitWidth, SourceSpan Span) : IntegerTypeNode(Name, BitWidth, false, Span);
public record SignedIntTypeNode(string Name, int BitWidth, SourceSpan Span) : IntegerTypeNode(Name, BitWidth, true, Span);
public record FloatTypeNode(string Name, int BitWidth, SourceSpan Span) : NumericTypeNode(Name, true, Span);

public record Int8TypeNode(SourceSpan Span) : SignedIntTypeNode(PrimitiveTypeNames.i8, 8, Span);
public record Int16TypeNode(SourceSpan Span) : SignedIntTypeNode(PrimitiveTypeNames.i16, 16, Span);
public record Int32TypeNode(SourceSpan Span) : SignedIntTypeNode(PrimitiveTypeNames.i32, 32, Span);
public record Int64TypeNode(SourceSpan Span) : SignedIntTypeNode(PrimitiveTypeNames.i64, 64, Span);
public record UInt8TypeNode(SourceSpan Span) : UnsignedIntTypeNode(PrimitiveTypeNames.u8, 8, Span);
public record UInt16TypeNode(SourceSpan Span) : UnsignedIntTypeNode(PrimitiveTypeNames.u16, 16, Span);
public record UInt32TypeTypeNode(SourceSpan Span) : UnsignedIntTypeNode(PrimitiveTypeNames.u32, 32, Span);
public record UInt64TypeNode(SourceSpan Span) : UnsignedIntTypeNode(PrimitiveTypeNames.u64, 64, Span);
public record Float32TypeNode(SourceSpan Span) : FloatTypeNode(PrimitiveTypeNames.f32, 32, Span);
public record Float64TypeNode(SourceSpan Span) : FloatTypeNode(PrimitiveTypeNames.f64, 64, Span);
public record BoolTypeNode(SourceSpan Span) : PrimitiveTypeNode(PrimitiveTypeNames.boolean, Span);
public record RuneTypeNode(SourceSpan Span) : PrimitiveTypeNode(PrimitiveTypeNames.rune, Span);

// Lowerable expressions
public record OnErrorExpressionNode(ExpressionNode Source, ExpressionNode Fallback, SourceSpan Span) : ExpressionNode(Span);
public record BubbleError(ExpressionNode Source, SourceSpan Span) : ExpressionNode(Span);