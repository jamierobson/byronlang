using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.Parser;

public partial class ByronHighLevelAstParser(List<Token> tokens)
{
    // ReSharper disable once RedundantDefaultMemberInitializer
    private int _activeTokenIndex = 0;

    public ProgramNode Parse()
    {
        var functions = new List<FunctionDeclarationNode>();
        var structs = new List<StructDeclarationNode>();
        var traits = new List<TraitDeclarationNode>();
        
        while (!IsAtEnd())
        {
            var token = Peek();
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (token.Kind)
            {
                case TokenKind.Fn:
                    functions.Add(ParseFunctionDeclaration(ScopeContext.Global));
                    break;
                case TokenKind.Implement:
                    functions.AddRange(ParseFunctionDeclarationsFromImplementBlock());
                    break;
                case TokenKind.Struct:
                    structs.Add(ParseStructDeclaration(ScopeContext.Global));
                    break;
                case TokenKind.Trait:
                    traits.Add(ParseTraitDeclaration());
                    break;
                default:
                    throw new ByronNotImplementedException(token.Kind, this, token.Span);
            }   
        }

        return new ProgramNode([..functions, ..structs, ..traits]);
    }

    private TraitDeclarationNode ParseTraitDeclaration()
    {
        var startNode = Consume(TokenKind.Trait, "Expected 'trait' block.");
        var identifier = Consume(TokenKind.Identifier, "Expected trait name");

        var (name, module) = NameAndModulePath(identifier);
        var traitTypeNode = new TraitTypeNode(name, module, identifier.Span);
        var scopeContext = new ScopeContext([], null, traitTypeNode);
        
        var (fields, functions) = ParseTraitMembers(scopeContext);
        return new TraitDeclarationNode(traitTypeNode, fields, functions, startNode.Span);
    }

    private (List<StructFieldNode> fields, List<FunctionSignatureNode> functions) ParseTraitMembers(ScopeContext context)
    {
        var fields = new List<StructFieldNode>();
        var functions = new List<FunctionSignatureNode>();
        _ = Consume(TokenKind.LBrace, "Expected '{'.");
        
        while (!IsAtEnd())
        {
            if (Peek().Kind == TokenKind.Fn)
            {
                Advance(); // skips the fn token
                var function = ParseFunctionSignature(context);
                functions.Add(function);
            }
            else
            {
                var field = ParseStructField(context);
                fields.Add(field);
            }
            
            
            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }
            _ = Consume(TokenKind.Comma, "Expected ',' separator between member declarations.");
            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }
        }
        
        _ = Consume(TokenKind.RBrace, "Expected '}'.");
        

        return (fields, functions);
    }

    private (string Name, string[] ModulePath) NameAndModulePath(Token identifierToken)
    {
        var maybeFullyQualifiedNameSegments = identifierToken.Lexeme.Split('.');
        var name =  maybeFullyQualifiedNameSegments[^1];
        var modulePath = maybeFullyQualifiedNameSegments[0..^1]; // todo: Make sure that module scope gets in here, when implemented.
        
        return (name, modulePath);
    }

    private List<FunctionDeclarationNode> ParseFunctionDeclarationsFromImplementBlock()
    {
        var implementFunctionDeclarations = new List<FunctionDeclarationNode>();
        var startDeclarationNode = Consume(TokenKind.Implement, "Expected 'implement' block.");
        var activeIdentifier = Consume(TokenKind.Identifier, "Expected identifier");

        TraitTypeNode? trait = null;
        
        if (ConsumingActiveTokenMatch(TokenKind.For))
        {
            var (traitName, traitModule) = NameAndModulePath(activeIdentifier);
            trait = new TraitTypeNode(traitName, traitModule, activeIdentifier.Span);
            activeIdentifier = Consume(TokenKind.Identifier, "Expected struct identifier after 'for' in trait implementation block");
        }
        
        var leftBrace = Consume(TokenKind.LBrace, "Expected '{'.");

        var (structName, structModule) = NameAndModulePath(activeIdentifier);
        var declaredType = new NominalTypeNode(structName, structModule, activeIdentifier.Span);
        var implementBlockDeclarationNode = new ImplementBlockDeclarationNode(declaredType, trait, ExpandSpan(startDeclarationNode, leftBrace));

        var context = new ScopeContext([], implementBlockDeclarationNode, null);
        
        while (!IsAtEnd())
        {
            if(ConsumingActiveTokenMatch(TokenKind.RBrace))
            {
                return implementFunctionDeclarations;
            }
            
            if (!ActiveTokenMatch(TokenKind.Fn))
            {
                throw new  ByronHighLevelParserException(Peek());
            }
            
            implementFunctionDeclarations.Add(ParseFunctionDeclaration(context));
        }
        
        _ = Consume(TokenKind.RBrace, "Expected '}'.");
        return implementFunctionDeclarations;
    }

    private StructDeclarationNode ParseStructDeclaration(ScopeContext context)
    {
        var structToken = Consume(TokenKind.Struct, "Expected 'struct'.");
        var nameToken = Consume(TokenKind.Identifier, "Expected struct name.");

        var fields = ParseStructFields(context);
        
        var (name, module) = NameAndModulePath(nameToken);
        var type = new NominalTypeNode(name, module, nameToken.Span);
        return new StructDeclarationNode(type, fields, ExpandSpan(structToken, Peek()));
    }

    private StructFieldNode ParseStructField(ScopeContext context)
    {
        var nameToken = Consume(TokenKind.Identifier, "Expected field name");
        _ = Consume(TokenKind.Colon, "Expected ':'.");
        var type = ParseTypeSignature(context, nameToken);
        return new StructFieldNode(nameToken.Lexeme, type, ExpandSpan(nameToken, type));
    }
    
    private List<StructFieldNode> ParseStructFields(ScopeContext context)
    {
        var fields = new List<StructFieldNode>();
        _ = Consume(TokenKind.LBrace, "Expected '{'.");

        while (!IsAtEnd())
        {
            var field = ParseStructField(context); 

            fields.Add(field);
            
            // todo: Make this better : A trailing comma is supported, and optional. 
            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }
            _ = Consume(TokenKind.Comma, "Expected ',' separator between field declarations.");
            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }
        }
        
        _ = Consume(TokenKind.RBrace, "Expected '}'.");
        return fields;
    }

    public FunctionSignatureNode ParseFunctionSignature(ScopeContext context)
    {
        var nameToken = Consume(TokenKind.Identifier, "Expected function name.");
        var parameters = ParseFunctionParameters(context); 
        _ = Consume(TokenKind.Colon, "Expected ':'.");
        var returnType = ParseTypeSignature(context, nameToken);
        
        return new FunctionSignatureNode(nameToken.Lexeme, parameters, returnType, ExpandSpan(nameToken, returnType));
    }
    
    public FunctionDeclarationNode ParseFunctionDeclaration(ScopeContext context)
    {
        var fnToken = Consume(TokenKind.Fn, "Expected 'fn'.");
        var functionSignature = ParseFunctionSignature(context);
        var body = ParseBlockStatement(context);

        var modulePath = context.RelativeModulePath();
        if (context.ImplementBlock?.TraitNode is not null)
        {
            modulePath = [..modulePath, ..context.ImplementBlock.TraitNode.ModulePath, context.ImplementBlock.TraitNode.Name];
        }
        
        return new FunctionDeclarationNode(modulePath, functionSignature, body, new SourceSpan(fnToken.Span.Line, fnToken.Span.Column, fnToken.Span.Start, body.Span.End));
    }

    private Token ParameterIdentifier(bool allowSelf)
    {
        if (allowSelf && ActiveTokenMatch(TokenKind.SelfReceiver))
        {
            return Advance();
        }
        
        return Consume(TokenKind.Identifier, "Expected parameter name.");
    }
    
    public List<ParameterNode> ParseFunctionParameters(ScopeContext context)
    {   
        _ = Consume(TokenKind.LParen, "Expected '('.");
        var parameters = new List<ParameterNode>();
        var parameterPosition = 0;
        if (!ActiveTokenMatch(TokenKind.RParen))
        {
            do
            {
                var parameterToken = ParameterIdentifier(context.ImplementBlock is not null && parameterPosition == 0);
                _ = Consume(TokenKind.Colon, "Expected ':'.");

                ReceiverBindingOwnership receiverBindingOwnership;
                
                if (ConsumingActiveTokenMatch(TokenKind.Take))
                {
                    receiverBindingOwnership = ReceiverBindingOwnership.Owned;
                }
                else if (Peek() is { Kind :TokenKind.Identifier})
                {
                    receiverBindingOwnership = ReceiverBindingOwnership.ImplicitCopy;
                } 
                else if (ConsumingActiveTokenMatch(TokenKind.Ampersand))
                {
                    receiverBindingOwnership = ConsumingActiveTokenMatch(TokenKind.Var) 
                        ? ReceiverBindingOwnership.MutableBorrow 
                        : ReceiverBindingOwnership.ImmutableBorrow;
                }
                else
                {
                    throw new ByronHighLevelParserException(Peek());
                }
                
                var parameterType = ParseTypeSignature(context, parameterToken);
                parameters.Add(new ParameterNode(receiverBindingOwnership, parameterToken.Lexeme, parameterType, ExpandSpan(parameterToken, parameterType)));
                parameterPosition++;
            } while (ConsumingActiveTokenMatch(TokenKind.Comma));
        }

        _ = Consume(TokenKind.RParen, "Expected ')'.");
        return parameters;
    }

    private TypeNode ParseTypeSignature(ScopeContext context, Token? identifierToken = null)
    {
        if (ConsumingActiveTokenMatch(TokenKind.Ampersand))
        {
            var ampersand = Previous();   
            var isMutable = ConsumingActiveTokenMatch(TokenKind.Var);
            var targetType = ParseTypeSignature(context, identifierToken);
            return new ReferenceTypeNode(targetType, isMutable, ExpandSpan(ampersand, targetType));
        }
        
        var token = Advance();

        if (TryGetPrimitive(token, out var primitive))
        {
            return primitive;
        }
        
        if (token.Kind == TokenKind.Identifier)
        {
            return ParseNominalTypeNode(token);
        }

        if (token.Kind == TokenKind.SelfType)
        {
            return ParseSelfTypeNode(context, identifierToken);
        }

        throw new ByronHighLevelParserException($"Unknown type signature target: {token.Lexeme}", token.Span);
    }

    private bool TryGetPrimitive(Token token, [NotNullWhen(true)]out PrimitiveTypeNode? type)
    {
        type = token.Lexeme switch
        {
            PrimitiveTypeNames.i8 => new Int8TypeNode(token.Span),
            PrimitiveTypeNames.i16 => new Int16TypeNode(token.Span),
            PrimitiveTypeNames.i32 => new Int32TypeNode(token.Span),
            PrimitiveTypeNames.i64 => new Int64TypeNode(token.Span),
            PrimitiveTypeNames.u8 => new UInt8TypeNode(token.Span),
            PrimitiveTypeNames.u16 => new UInt16TypeNode(token.Span),
            PrimitiveTypeNames.u32 => new UInt32TypeTypeNode(token.Span),
            PrimitiveTypeNames.u64 => new UInt64TypeNode(token.Span),
            PrimitiveTypeNames.f32 => new Float32TypeNode(token.Span),
            PrimitiveTypeNames.f64 => new Float64TypeNode(token.Span),
            PrimitiveTypeNames.boolean => new BoolTypeNode(token.Span),
            PrimitiveTypeNames.@void => new VoidTypeNode(token.Span),
            PrimitiveTypeNames.rune => new RuneTypeNode(token.Span),
            _ => null
        };

        return type is not null;
    }

    private SelfTypeNode ParseSelfTypeNode(ScopeContext context, Token? identifierToken = null)
    {
        if (context.ImplementBlock is null && context.TraitDeclaration is null)
        {
            throw new ByronHighLevelParserException("The 'Self' type is only valid in implementation block function signatures", Peek().Span);
        }
        
        if (identifierToken is null)
        {
            throw new ByronHighLevelParserException("The self parameter name must be bound to a valid 'Self' type", context.ImplementBlock?.Span ?? context.TraitDeclaration!.Span);
        }

        TypeNode selfType = (context.ImplementBlock is not null)
            ? context.ImplementBlock.TypeNode
            : context.TraitDeclaration!;
        
        return new  SelfTypeNode(selfType, identifierToken.Span);
    }
    
    private NominalTypeNode ParseNominalTypeNode(Token firstIdentifier)
    {
        var modulePathSegments = new List<string>();
        var endToken = firstIdentifier;

        while (ConsumingActiveTokenMatch(TokenKind.Dot))
        {
            var segment = Consume(TokenKind.Identifier, "Expected identifier after '.' in type path.");
            modulePathSegments.Add(segment.Lexeme);
            endToken = segment;
        }

        var name = firstIdentifier.Lexeme;
        var path = modulePathSegments.Count == 0 ? [] : modulePathSegments[0..^1].ToArray();

        return new NominalTypeNode(name, path, ExpandSpan(firstIdentifier, endToken));
    }

    private Token Advance()
    {
        if (!IsAtEnd())
        {
            _activeTokenIndex++;
        }
        return Previous();
    }

    private bool ConsumingActiveTokenMatch(TokenKind kind)
    {
        if (ActiveTokenMatch(kind))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool ActiveTokenMatch(TokenKind kind) => !IsAtEnd() && Peek().Kind == kind;
    private Token Peek() => tokens[_activeTokenIndex];
    private Token Previous() => tokens[_activeTokenIndex - 1];
    private bool IsAtEnd() => _activeTokenIndex >= tokens.Count || Peek().Kind == TokenKind.Eof;
    private Token Consume(TokenKind kind, string error) => ActiveTokenMatch(kind) ? Advance() : throw new ByronHighLevelParserException(error, _activeTokenIndex > 0 ? Previous().Span : Peek().Span);
    private SourceSpan ExpandSpan(Token firstToken, Token endToken) => ExpandSpan(firstToken.Span, endToken.Span);
    private SourceSpan ExpandSpan(AstNode node, Token endToken) => ExpandSpan(node.Span, endToken.Span);
    private SourceSpan ExpandSpan(Token firstToken, AstNode endNode) => ExpandSpan(firstToken.Span, endNode.Span);
    private SourceSpan ExpandSpan(SourceSpan start, SourceSpan end) => start with { End = end.End };
}
