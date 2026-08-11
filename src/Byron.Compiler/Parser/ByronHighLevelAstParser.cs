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
                default:
                    throw new ByronNotImplementedException(token.Kind, this, token.Span);
            }   
        }

        return new ProgramNode([..functions, ..structs]);
    }

    private List<FunctionDeclarationNode> ParseFunctionDeclarationsFromImplementBlock()
    {
        var implementFunctionDeclarations = new List<FunctionDeclarationNode>();
        var startDeclarationNode = Consume(TokenKind.Implement, "Expected 'implement' block.");
        var identifierToken = Consume(TokenKind.Identifier, "Expected identifier");
        _ = Consume(TokenKind.LBrace, "Expected '{'.");
        
        var maybeFullyQualifiedNameSegments = identifierToken.Lexeme.Split('.');
        var name =  maybeFullyQualifiedNameSegments[^1];
        var modulePath = maybeFullyQualifiedNameSegments[0..^1]; // todo: Make sure that module scope gets in here, when implemented.

        var declaredType = new NominalTypeNode(name, modulePath, identifierToken.Span);
        var implementBlockDeclarationNode = new ImplementBlockDeclarationNode(declaredType, ExpandSpan(startDeclarationNode, identifierToken));

        var context = new ScopeContext([], implementBlockDeclarationNode);
        
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
        
        return new StructDeclarationNode(nameToken.Lexeme, [], fields, ExpandSpan(structToken, Peek()));
    }

    private List<StructFieldNode> ParseStructFields(ScopeContext context)
    {
        var fields = new List<StructFieldNode>();
        _ = Consume(TokenKind.LBrace, "Expected '{'.");

        while (!ActiveTokenMatch(TokenKind.RBrace))
        {
            var nameToken = Consume(TokenKind.Identifier, "Expected field name");
            _ = Consume(TokenKind.Colon, "Expected ':'.");
            var type = ParseTypeSignature(context, nameToken);

            fields.Add(new StructFieldNode(nameToken.Lexeme, type, ExpandSpan(nameToken, type)));

            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }
            _ = Consume(TokenKind.Comma, "Expected ',' separator between field declarations.");
        }
        
        Advance();
        return fields;
    }

    public FunctionDeclarationNode ParseFunctionDeclaration(ScopeContext context)
    {
        var fnToken = Consume(TokenKind.Fn, "Expected 'fn'.");
        var nameToken = Consume(TokenKind.Identifier, "Expected function name.");

        var parameters = ParseFunctionParameters(context); 
        _ = Consume(TokenKind.Colon, "Expected ':'.");
        var returnType = ParseTypeSignature(context, nameToken);
        var body = ParseBlockStatement(context);

         
        return new FunctionDeclarationNode(nameToken.Lexeme, context.RelativeModulePath(), parameters, returnType, body, new SourceSpan(fnToken.Span.Line, fnToken.Span.Column, fnToken.Span.Start, body.Span.End));
    }

    private Token ParameterIdentifier(ScopeContext context)
    {
        if (context.ImplementBlock != null && ActiveTokenMatch(TokenKind.SelfReceiver))
        {
            return Advance();
        }
        
        return Consume(TokenKind.Identifier, "Expected parameter name.");
    }
    
    public List<ParameterNode> ParseFunctionParameters(ScopeContext context)
    {   
        _ = Consume(TokenKind.LParen, "Expected '('.");
        var parameters = new List<ParameterNode>();
        if (!ActiveTokenMatch(TokenKind.RParen))
        {
            do
            {
                var parameterToken = ParameterIdentifier(context);
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

    private NominalTypeNode ParseSelfTypeNode(ScopeContext context, Token? identifierToken = null)
    {
        if (context.ImplementBlock is null)
        {
            throw new ByronHighLevelParserException("The 'Self' type is only valid in implementation block function signatures", Peek().Span);
        }
        
        if (identifierToken is null)
        {
            throw new ByronHighLevelParserException("The self parameter name must be bound to a valid 'Self' type", context.ImplementBlock.Span);
        }
        
        return new NominalTypeNode(context.ImplementBlock.Name, context.ImplementBlock.ModulePath, context.ImplementBlock.Span);
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
    private Token Consume(TokenKind kind, string error) => ActiveTokenMatch(kind) ? Advance() : throw new ByronHighLevelParserException(error, Previous().Span);
    private SourceSpan ExpandSpan(Token firstToken, Token endToken) => ExpandSpan(firstToken.Span, endToken.Span);
    private SourceSpan ExpandSpan(AstNode node, Token endToken) => ExpandSpan(node.Span, endToken.Span);
    private SourceSpan ExpandSpan(Token firstToken, AstNode endNode) => ExpandSpan(firstToken.Span, endNode.Span);
    private SourceSpan ExpandSpan(SourceSpan start, SourceSpan end) => start with { End = end.End };
}
