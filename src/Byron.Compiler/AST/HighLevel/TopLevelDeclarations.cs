using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST.HighLevel;

public abstract class TopLevelDeclarationNode : AstNode
{
    public string Name { get; init; }
    public string[] ModulePath { get; init; }
    public CanonicalName CanonicalName => field ??= new(ModulePath, Name);

    protected TopLevelDeclarationNode(string name, string[] modulePath, SourceSpan span) : base(span)
    {
        Name = name;
        ModulePath = modulePath;
    }
}

public class ImplementBlockDeclarationNode : TopLevelDeclarationNode
{
    public NominalTypeNode TypeNode { get; init; }

    public ImplementBlockDeclarationNode(NominalTypeNode typeNode, SourceSpan span)
        : base(typeNode.Name, typeNode.ModulePath, span)
    {
        TypeNode = typeNode;
    }
}

public class FunctionDeclarationNode : TopLevelDeclarationNode
{
    public List<ParameterNode> Parameters { get; init; }
    public TypeNode ReturnType { get; init; }
    public BlockStatementNode Body { get; init; }

    public FunctionDeclarationNode(string name, string[] modulePath, List<ParameterNode> parameters, TypeNode returnType, BlockStatementNode body, SourceSpan span)
        : base(name, modulePath, span)
    {
        Parameters = parameters;
        ReturnType = returnType;
        Body = body;
    }
}

public class ParameterNode : AstNode
{
    public ReceiverBindingOwnership Ownership { get; init; }
    public string Name { get; init; }
    public TypeNode Type { get; init; }

    public ParameterNode(ReceiverBindingOwnership ownership, string name, TypeNode type, SourceSpan span) : base(span)
    {
        Ownership = ownership;
        Name = name;
        Type = type;
    }
}

public class StructDeclarationNode : TopLevelDeclarationNode
{
    public List<StructFieldNode> Fields { get; init; }

    public StructDeclarationNode(string name, string[] modulePath, List<StructFieldNode> fields, SourceSpan span)
        : base(name, modulePath, span)
    {
        Fields = fields;
    }
}

public class StructFieldNode : AstNode
{
    public string Name { get; init; }
    public TypeNode Type { get; init; }

    public StructFieldNode(string name, TypeNode type, SourceSpan span) : base(span)
    {
        Name = name;
        Type = type;
    }
}

public class StructFieldInitializerNode : AstNode
{
    public string FieldName { get; init; }
    public ExpressionNode Value { get; set; }

    public StructFieldInitializerNode(string fieldName, ExpressionNode value, SourceSpan span) : base(span)
    {
        FieldName = fieldName;
        Value = value;
    }
}