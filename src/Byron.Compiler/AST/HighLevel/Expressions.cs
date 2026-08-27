using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST.HighLevel;

public abstract class ExpressionNode : AstNode
{
    protected ExpressionNode(SourceSpan span) : base(span) { }
}

public class LiteralExpressionNode<T>(T value, SourceSpan span) : ExpressionNode(span)
    where T : struct
{
    public T Value { get; init; } = value;
}

public class IntegerLiteralNode(long value, SourceSpan span) : LiteralExpressionNode<long>(value, span);

public class FloatLiteralNode(double value, SourceSpan span) : LiteralExpressionNode<double>(value, span);

public class BooleanLiteralNode(bool value, SourceSpan span) : LiteralExpressionNode<bool>(value, span);

public class VariableExpressionNode(string name, SourceSpan span) : ExpressionNode(span)
{
    public string Name { get; set; } = name;
}

public record IdentifierSegment(string Name, SourceSpan Span); 

public class PathAccessExpressionNode(IdentifierSegment[] identifierSegments, SourceSpan span) : ExpressionNode(span)
{
    public readonly IdentifierSegment[] IdentifierSegments = identifierSegments;
    public string[] Path => field ??= IdentifierSegments.Select(s => s.Name).ToArray();
}

public class FunctionInvocationVariableExpressionNode(
    FunctionDeclarationNode function,
    SourceSpan span)
    : VariableExpressionNode(function.Symbol.MemberName, span)
{
    public FunctionDeclarationNode Function { get; } = function;
}

public class AddressOfExpressionNode : ExpressionNode
{
    public ExpressionNode Target { get; set; }
    public bool IsMutable { get; init; }

    public AddressOfExpressionNode(ExpressionNode target, bool isMutable, SourceSpan span) : base(span)
    {
        Target = target;
        IsMutable = isMutable;
    }
}

public class DereferenceExpressionNode : ExpressionNode
{
    public ExpressionNode Target { get; set; }

    public DereferenceExpressionNode(ExpressionNode target, SourceSpan span) : base(span)
    {
        Target = target;
    }
}

public abstract class CallExpressionNode : ExpressionNode
{
    public ExpressionNode Callee { get; set; }
    public List<ExpressionNode> Arguments { get; set; }

    protected CallExpressionNode(ExpressionNode callee, List<ExpressionNode> arguments, SourceSpan span) : base(span)
    {
        Callee = callee;
        Arguments = arguments;
    }
}

public class FreeFunctionCallExpressionNode : CallExpressionNode
{
    public FreeFunctionCallExpressionNode(ExpressionNode callee, List<ExpressionNode> arguments, SourceSpan span)
        : base(callee, arguments, span) { }
}

public class MethodCallExpression : CallExpressionNode
{
    public ExpressionNode Receiver { get; set; }

    public MethodCallExpression(ExpressionNode receiver, FunctionInvocationVariableExpressionNode callee, List<ExpressionNode> arguments, SourceSpan span) : base(callee, arguments, span)
    {
        Receiver = receiver;
    }
}

public class BinaryExpressionNode : ExpressionNode
{
    public ExpressionNode Left { get; set; }
    public BinaryOperator Operator { get; init; }
    public ExpressionNode Right { get; set; }

    public BinaryExpressionNode(ExpressionNode left, BinaryOperator op, ExpressionNode right, SourceSpan span)
        : base(span)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}

public class StructFieldInitializationExpressionNode : ExpressionNode
{
    public NominalTypeNode NominalType { get; set; }
    public List<StructFieldInitializerNode> FieldInitializers { get; init; }

    public StructFieldInitializationExpressionNode(NominalTypeNode nominalType, List<StructFieldInitializerNode> fieldInitializers, SourceSpan span)
        : base(span)
    {
        NominalType = nominalType;
        FieldInitializers = fieldInitializers;
    }
}

public class MemberAccessExpressionNode : ExpressionNode
{
    public ExpressionNode Target { get; set; }
    public string MemberName { get; set; }

    public MemberAccessExpressionNode(ExpressionNode target, string memberName, SourceSpan span) : base(span)
    {
        Target = target;
        MemberName = memberName;
    }
}

public class UnaryExpressionNode : ExpressionNode
{
    public UnaryOperator Operator { get; init; }
    public ExpressionNode Operand { get; set; }

    public UnaryExpressionNode(UnaryOperator op, ExpressionNode operand, SourceSpan span) : base(span)
    {
        Operator = op;
        Operand = operand;
    }
}

// Casts
public abstract class CastExpressionNode : ExpressionNode
{
    public ExpressionNode Operand { get; set; }
    public TypeNode TargetType { get; init; }

    protected CastExpressionNode(ExpressionNode operand, TypeNode targetType, SourceSpan span) : base(span)
    {
        Operand = operand;
        TargetType = targetType;
    }
}

public class ExtendIntegerNode : CastExpressionNode
{
    public new IntegerTypeNode TargetType => (IntegerTypeNode)base.TargetType;

    public ExtendIntegerNode(ExpressionNode operand, IntegerTypeNode targetType, SourceSpan span)
        : base(operand, targetType, span) { }
}

public class ExtendFloatNode : CastExpressionNode
{
    public new FloatTypeNode TargetType => (FloatTypeNode)base.TargetType;

    public ExtendFloatNode(ExpressionNode operand, FloatTypeNode targetType, SourceSpan span)
        : base(operand, targetType, span) { }
}

public class CastIntToFloatNode : CastExpressionNode
{
    public new FloatTypeNode TargetType => (FloatTypeNode)base.TargetType;
    public bool SourceTypeIsSigned { get; init; }

    public CastIntToFloatNode(ExpressionNode operand, FloatTypeNode targetType, bool sourceTypeIsSigned, SourceSpan span)
        : base(operand, targetType, span)
    {
        SourceTypeIsSigned = sourceTypeIsSigned;
    }
}

public class CastFloatToIntNode : CastExpressionNode
{
    public new IntegerTypeNode TargetType => (IntegerTypeNode)base.TargetType;
    public bool IsSigned { get; init; }

    public CastFloatToIntNode(ExpressionNode operand, IntegerTypeNode targetType, bool isSigned, SourceSpan span)
        : base(operand, targetType, span)
    {
        IsSigned = isSigned;
    }
}

// Lowerable expressions
public class OnErrorExpressionNode : ExpressionNode
{
    public ExpressionNode Source { get; set; }
    public ExpressionNode Fallback { get; set; }

    public OnErrorExpressionNode(ExpressionNode source, ExpressionNode fallback, SourceSpan span) : base(span)
    {
        Source = source;
        Fallback = fallback;
    }
}

public class BubbleError : ExpressionNode
{
    public ExpressionNode Source { get; set; }

    public BubbleError(ExpressionNode source, SourceSpan span) : base(span)
    {
        Source = source;
    }
}