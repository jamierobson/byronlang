using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST.HighLevel;

public abstract class StatementNode : AstNode
{
    protected StatementNode(SourceSpan span) : base(span) { }
}

public class BlockStatementNode : StatementNode
{
    public List<StatementNode> Statements { get; init; }

    public BlockStatementNode(List<StatementNode> statements, SourceSpan span) : base(span)
    {
        Statements = statements;
    }
}

public class ReturnStatementNode : StatementNode
{
    public ExpressionNode? Expression { get; set; }

    public ReturnStatementNode(ExpressionNode? expression, SourceSpan span) : base(span)
    {
        Expression = expression;
    }
}

public class YieldStatementNode : StatementNode
{
    public ExpressionNode Expression { get; set; }

    public YieldStatementNode(ExpressionNode expression, SourceSpan span) : base(span)
    {
        Expression = expression;
    }
}

public class DiscardStatementNode : StatementNode
{
    public ExpressionNode Initializer { get; set; }

    public DiscardStatementNode(ExpressionNode initializer, SourceSpan span) : base(span)
    {
        Initializer = initializer;
    }
}

public class VariableDeclarationNode : StatementNode
{
    public bool IsMutable { get; init; }
    public string Name { get; init; }
    public TypeNode? TypeAnnotation { get; set; }
    public ExpressionNode Initializer { get; set; }

    public VariableDeclarationNode(bool isMutable, string name, TypeNode? typeAnnotation, ExpressionNode initializer, SourceSpan span) : base(span)
    {
        IsMutable = isMutable;
        Name = name;
        TypeAnnotation = typeAnnotation;
        Initializer = initializer;
    }
}

public class AssignmentStatementNode : StatementNode
{
    public ExpressionNode Target { get; set; }
    public ExpressionNode Value { get; set; }

    public AssignmentStatementNode(ExpressionNode target, ExpressionNode value, SourceSpan span) : base(span)
    {
        Target = target;
        Value = value;
    }
}

public class ExpressionStatementNode : StatementNode
{
    public ExpressionNode Expression { get; set; }

    public ExpressionStatementNode(ExpressionNode expression, SourceSpan span) : base(span)
    {
        Expression = expression;
    }
}

public class IfElseStatement : StatementNode
{
    public ExpressionNode Condition { get; set; }
    public BlockStatementNode ThenBranch { get; init; }
    public BlockStatementNode? ElseBranch { get; init; }

    public IfElseStatement(ExpressionNode condition, BlockStatementNode thenBranch, BlockStatementNode? elseBranch, SourceSpan span) : base(span)
    {
        Condition = condition;
        ThenBranch = thenBranch;
        ElseBranch = elseBranch;
    }
}

public class WhileStatement : StatementNode
{
    public ExpressionNode ContinuationCondition { get; set; }
    public BlockStatementNode Body { get; init; }

    public WhileStatement(ExpressionNode continuationCondition, BlockStatementNode body, SourceSpan span) : base(span)
    {
        ContinuationCondition = continuationCondition;
        Body = body;
    }
}

public class BreakStatement : StatementNode
{
    public BreakStatement(SourceSpan span) : base(span) { }
}

public class ContinueStatement : StatementNode
{
    public ContinueStatement(SourceSpan span) : base(span) { }
}