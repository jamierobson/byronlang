using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST.HighLevel;

public abstract class TopLevelDeclarationNode : AstNode
{
    public Symbol Symbol { get; set; }

    protected TopLevelDeclarationNode(string name, SourceSpan span) : base(span)
    {
        if(string.IsNullOrEmpty(name))
        {
            throw new ArgumentException($"{nameof(name)} must not be null when defining a symbol");
        }
        Symbol = new Symbol([..name.Split('.')]);
    }

    protected TopLevelDeclarationNode(string[] symbolSegments, SourceSpan span) : base(span)
    {
        Symbol = new Symbol(symbolSegments);
    }
    
    protected TopLevelDeclarationNode(Symbol symbol, SourceSpan span) : base(span)
    {
        Symbol = symbol;
    }
}

public class ImplementBlockDeclarationNode : TopLevelDeclarationNode
{
    public void UpdateType(NominalTypeNode typeNode)
    {
        TypeNode = typeNode;
        Symbol = typeNode.Symbol;
    }
    public TraitTypeNode? TraitNode { get; set; }
    public NominalTypeNode TypeNode { get; set; }
    public List<FunctionDeclarationNode> FunctionDeclarations { get; } = new();

    public ImplementBlockDeclarationNode(NominalTypeNode typeNode, TraitTypeNode? traitNode, SourceSpan span)
        : base(typeNode.Symbol, span)
    {
        TypeNode = typeNode;
        TraitNode = traitNode;
    }
}

public class FunctionDeclarationNode : TopLevelDeclarationNode
{
    public FunctionSignatureNode Signature { get; init; }
    public BlockStatementNode Body { get; init; }

    public FunctionDeclarationNode(FunctionSignatureNode signature, BlockStatementNode body, SourceSpan span)
        : base(signature.Name, span)
    {
        Signature =  signature;
        Body = body;
    }
}

public class FunctionSignatureNode : AstNode
{
    public static string EntryFunctionName = "main";
    
    public string Name { get; init; }
    public List<ParameterNode> Parameters { get; init; }
    public TypeNode ReturnType { get; set; }
    public FunctionSignatureNode(string name, List<ParameterNode> parameters, TypeNode returnType, SourceSpan span) : base(span)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
    }
}

public class ParameterNode : AstNode
{
    public const string SelfArgumentName = "self";
    public ReceiverBindingOwnership Ownership { get; init; }
    public string Name { get; init; }
    public TypeNode Type { get; set; }

    public ParameterNode(ReceiverBindingOwnership ownership, string name, TypeNode type, SourceSpan span) : base(span)
    {
        Ownership = ownership;
        Name = name;
        Type = type;
    }
}

public class AliasDeclarationNode(string name, Symbol symbol, SourceSpan span) : TopLevelDeclarationNode(symbol, span)
{
    public void UpdateAliasingSymbol(Symbol symbol)
    {
        Symbol = symbol;
    }
    public Symbol AliasingSymbol => Symbol;
    public string Name = name;
}

public class TraitDeclarationNode : TopLevelDeclarationNode
{
    public TraitTypeNode Type { get; init; }
    public List<StructFieldNode> RequiredFields { get; init; }
    public List<FunctionSignatureNode> RequiredFunctions { get; init; }
    public TraitDeclarationNode(TraitTypeNode type, List<StructFieldNode> fields, List<FunctionSignatureNode> functions, SourceSpan span) : base(type.Symbol, span)
    {
        RequiredFields = fields;
        RequiredFunctions = functions;
        Type = type;
    }
}

public class StructDeclarationNode : TopLevelDeclarationNode
{
    public void UpdateSymbol(Symbol symbol)
    {
        Symbol = symbol;
        Type.Symbol = symbol;
    }
    public NominalTypeNode Type { get; }
    public List<StructFieldNode> Fields { get; init; }

    public StructDeclarationNode(NominalTypeNode type, List<StructFieldNode> fields, SourceSpan span)
        : base(type.Symbol, span)
    {
        Type = type;
        Fields = fields;
    }
}

public class StructFieldNode : AstNode
{
    public string Name { get; init; }
    public TypeNode Type { get; set; }

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