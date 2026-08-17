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
    public TraitTypeNode? TraitNode { get; init; }
    public NominalTypeNode TypeNode { get; init; }

    public ImplementBlockDeclarationNode(NominalTypeNode typeNode, TraitTypeNode? traitNode, SourceSpan span)
        : base(typeNode.Name, typeNode.ModulePath, span)
    {
        TypeNode = typeNode;
        TraitNode = traitNode;
    }
}

public class FunctionDeclarationNode : TopLevelDeclarationNode
{
    public FunctionSignatureNode Signature { get; init; }
    public BlockStatementNode Body { get; init; }

    public FunctionDeclarationNode(string[] modulePath, FunctionSignatureNode signature, BlockStatementNode body, SourceSpan span)
        : base(signature.Name, modulePath, span)
    {
        Signature =  signature;
        Body = body;
    }
}

public class FunctionSignatureNode : AstNode
{
    public string Name { get; init; }
    public List<ParameterNode> Parameters { get; init; }
    public TypeNode ReturnType { get; init; }
    public FunctionSignatureNode(string name, List<ParameterNode> parameters, TypeNode returnType, SourceSpan span) : base(span)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
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

public class TraitDeclarationNode : TopLevelDeclarationNode
{
    public TraitDeclarationNode(string name, string[] modulePath, List<StructFieldNode> fields, List<FunctionSignatureNode> functions, SourceSpan span) : base(name , modulePath, span)
    {
        RequiredFields = fields;
        RequiredFunctions = functions;
    }
    public List<StructFieldNode> RequiredFields { get; init; }
    public List<FunctionSignatureNode> RequiredFunctions { get; init; }
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