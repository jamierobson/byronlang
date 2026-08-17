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

    protected TopLevelDeclarationNode(CanonicalName canonicalName, SourceSpan span) : base(span)
    {
        CanonicalName =  canonicalName;
        Name = canonicalName.ShortName;
        ModulePath = canonicalName.ModulePath;
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
    public TraitTypeNode Type { get; init; }
    public List<StructFieldNode> RequiredFields { get; init; }
    public List<FunctionSignatureNode> RequiredFunctions { get; init; }
    public TraitDeclarationNode(TraitTypeNode type, List<StructFieldNode> fields, List<FunctionSignatureNode> functions, SourceSpan span) : base(type.CanonicalName, span)
    {
        RequiredFields = fields;
        RequiredFunctions = functions;
        Type = type;
    }
}

public class StructDeclarationNode : TopLevelDeclarationNode
{
    public NominalTypeNode Type { get; }
    public List<StructFieldNode> Fields { get; init; }

    public StructDeclarationNode(NominalTypeNode type, List<StructFieldNode> fields, SourceSpan span)
        : base(type.Name, type.ModulePath, span)
    {
        Type = type;
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