using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.Parser;

public partial class ByronHighLevelAstParser(TokenizedFile tokenizedFile)
{
    // ReSharper disable once RedundantDefaultMemberInitializer
    private int _activeTokenIndex = 0;

    public ProgramNode Parse()
    {
        var fileName = Path.GetFileNameWithoutExtension(tokenizedFile.FilePath);
        var fileModule = new FileModuleNode(fileName, ExpandSpan(tokenizedFile.Tokens[0], tokenizedFile.Tokens[^1]));
        ParseModuleDeclarations(fileModule.Declarations);
        return new ProgramNode([fileModule]);
    }

    private void ParseModuleDeclarations(ModuleDeclarationCollection declarations)
    {
        while (!IsAtEnd())
        {
            var token = Peek();
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (token.Kind)
            {
                case TokenKind.RBrace:
                    // Ending this context
                    Advance();
                    return;
                case TokenKind.Fn:
                    declarations.Functions.Add(ParseFunctionDeclaration(null));
                    continue;
                case TokenKind.Implement:
                    declarations.ImplementBlocks.Add(ParseImplementBlock());
                    continue;
                case TokenKind.Struct:
                    declarations.Structs.Add(ParseStructDeclaration());
                    continue;
                case TokenKind.Trait:
                    declarations.Traits.Add(ParseTraitDeclaration());
                    continue;
                case TokenKind.Module:
                    var module = ModuleBlock();
                    declarations.ChildModules.Add(module);
                    ParseModuleDeclarations(module.Declarations);
                    continue;
                default:
                    throw new ByronNotImplementedException(token.Kind, this, token.Span);
            }
        }
    }

    private BlockModuleNode ModuleBlock()
    {
        _ = Consume(TokenKind.Module, "Expected 'module' block.");
        var identifier = Consume(TokenKind.Identifier, "Expected identifier");
        _ = Consume(TokenKind.LBrace, "Expected '{'.");
        
        return new BlockModuleNode(identifier.Lexeme, identifier.Span);
    }

    private TraitDeclarationNode ParseTraitDeclaration()
    {
        var startNode = Consume(TokenKind.Trait, "Expected 'trait' block.");
        var identifier = Consume(TokenKind.Identifier, "Expected trait name");

        var traitTypeNode = new TraitTypeNode(identifier.Lexeme, identifier.Span);

        var (fields, functions) = ParseTraitMembers(traitTypeNode);
        return new TraitDeclarationNode(traitTypeNode, fields, functions, startNode.Span);
    }

    private (List<StructFieldNode> fields, List<FunctionSignatureNode> functions) ParseTraitMembers(TraitTypeNode traitTypeNode)
    {
        var fields = new List<StructFieldNode>();
        var functions = new List<FunctionSignatureNode>();
        _ = Consume(TokenKind.LBrace, "Expected '{'.");
        var selfType = SelfTypeContext.From(traitTypeNode);
        
        while (!IsAtEnd())
        {
            if (Peek().Kind == TokenKind.Fn)
            {
                Advance(); // skips the fn token
                var function = ParseFunctionSignature(selfType);
                functions.Add(function);
            }
            else
            {
                var field = ParseStructField();
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
    
    private ImplementBlockDeclarationNode ParseImplementBlock()
    {
        var startDeclarationNode = Consume(TokenKind.Implement, "Expected 'implement' block.");
        var activeIdentifier = Consume(TokenKind.Identifier, "Expected identifier");

        TraitTypeNode? trait = null;
        
        if (ConsumingActiveTokenMatch(TokenKind.For))
        {
            trait = new TraitTypeNode(activeIdentifier.Lexeme, activeIdentifier.Span);
            activeIdentifier = Consume(TokenKind.Identifier, "Expected struct identifier after 'for' in trait implementation block");
        }
        
        var leftBrace = Consume(TokenKind.LBrace, "Expected '{'.");
        
        var declaredType = new NominalTypeNode(activeIdentifier.Lexeme, activeIdentifier.Span);
        var implementBlockDeclarationNode = new ImplementBlockDeclarationNode(declaredType, trait, ExpandSpan(startDeclarationNode, leftBrace));
        var selfType = SelfTypeContext.From(implementBlockDeclarationNode);

        while (!IsAtEnd())
        {
            var token = Peek();
            
            switch (token.Kind)
            {                
                case TokenKind.RBrace:
                    Advance();
                    return implementBlockDeclarationNode;
                case TokenKind.Fn:
                    implementBlockDeclarationNode.FunctionDeclarations.Add(ParseFunctionDeclaration(selfType));
                    break;
                default:
                    throw new ByronNotImplementedException(token.Kind, this, token.Span);
                    
            }
        }

        return implementBlockDeclarationNode;
    }

    private StructDeclarationNode ParseStructDeclaration()
    {
        var structToken = Consume(TokenKind.Struct, "Expected 'struct'.");
        var nameToken = Consume(TokenKind.Identifier, "Expected struct name.");

        var fields = ParseStructFields();
        
        var type = new NominalTypeNode(nameToken.Lexeme, nameToken.Span);
        return new StructDeclarationNode(type, fields, ExpandSpan(structToken, Peek()));
    }

    private StructFieldNode ParseStructField()
    {
        var nameToken = Consume(TokenKind.Identifier, "Expected field name");
        _ = Consume(TokenKind.Colon, "Expected ':'.");
        var type = ParseTypeSignature(null, nameToken);
        return new StructFieldNode(nameToken.Lexeme, type, ExpandSpan(nameToken, type));
    }
    
    private List<StructFieldNode> ParseStructFields()
    {
        var fields = new List<StructFieldNode>();
        _ = Consume(TokenKind.LBrace, "Expected '{'.");

        while (!IsAtEnd())
        {
            var field = ParseStructField(); 

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

    private FunctionSignatureNode ParseFunctionSignature(SelfTypeContext? self)
    {
        var nameToken = Consume(TokenKind.Identifier, "Expected function name.");
        var parameters = ParseFunctionParameters(self); 
        _ = Consume(TokenKind.Colon, "Expected ':'.");
        var returnType = ParseTypeSignature(self, nameToken);
        
        return new FunctionSignatureNode(nameToken.Lexeme, parameters, returnType, ExpandSpan(nameToken, returnType));
    }
    
    public FunctionDeclarationNode ParseFunctionDeclaration(SelfTypeContext? self)
    {
        var fnToken = Consume(TokenKind.Fn, "Expected 'fn'.");
        var functionSignature = ParseFunctionSignature(self);
        var body = ParseBlockStatement(self);
        
        var functionDeclaration = new FunctionDeclarationNode(functionSignature, body, ExpandSpan(fnToken, body));
        return functionDeclaration;
    }

    private Token ParameterIdentifier(bool allowSelf)
    {
        if (allowSelf && ActiveTokenMatch(TokenKind.SelfReceiver))
        {
            return Advance();
        }
        
        return Consume(TokenKind.Identifier, "Expected parameter name.");
    }
    
    public List<ParameterNode> ParseFunctionParameters(SelfTypeContext? self)
    {   
        _ = Consume(TokenKind.LParen, "Expected '('.");
        var parameters = new List<ParameterNode>();
        var parameterPosition = 0;
        if (!ActiveTokenMatch(TokenKind.RParen))
        {
            do
            {
                var parameterToken = ParameterIdentifier(self is not null && parameterPosition == 0);
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
                
                var parameterType = ParseTypeSignature(self, parameterToken);
                parameters.Add(new ParameterNode(receiverBindingOwnership, parameterToken.Lexeme, parameterType, ExpandSpan(parameterToken, parameterType)));
                parameterPosition++;
            } while (ConsumingActiveTokenMatch(TokenKind.Comma));
        }

        _ = Consume(TokenKind.RParen, "Expected ')'.");
        return parameters;
    }

    private TypeNode ParseTypeSignature(SelfTypeContext? selfType, Token identifierToken)
    {
        if (ConsumingActiveTokenMatch(TokenKind.Ampersand))
        {
            var ampersand = Previous();   
            var isMutable = ConsumingActiveTokenMatch(TokenKind.Var);
            var targetType = ParseTypeSignature(selfType, identifierToken);
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
            return ParseSelfTypeNode(selfType, identifierToken.Span);
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

    private SelfTypeNode ParseSelfTypeNode(SelfTypeContext? context, SourceSpan sourceSpan)
    {
        if (context?.ImplementBlock is null && context?.TraitDeclaration is null)
        {
            throw new ByronHighLevelParserException(
                "The 'Self' type is only valid in an implementation block or in a trait function declaration", sourceSpan);
        }
        
        return new SelfTypeNode(context.GetSelfType(sourceSpan), sourceSpan);
    }
    
    private NominalTypeNode ParseNominalTypeNode(Token firstIdentifierSegment)
    {
        var modulePathSegments = new List<string> {firstIdentifierSegment.Lexeme};
        var endToken = firstIdentifierSegment;

        if (!ActiveTokenMatch(TokenKind.Dot))
        {
            return new NominalTypeNode(firstIdentifierSegment.Lexeme, firstIdentifierSegment.Span);
        }
        
        while (ConsumingActiveTokenMatch(TokenKind.Dot))
        {
            var segment = Consume(TokenKind.Identifier, "Expected identifier after '.' in type path.");
            modulePathSegments.Add(segment.Lexeme);
            endToken = segment;
        }

        return new NominalTypeNode([..modulePathSegments], ExpandSpan(firstIdentifierSegment, endToken));
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
    private Token Peek() => tokenizedFile.Tokens[_activeTokenIndex];
    private Token Previous() => tokenizedFile.Tokens[_activeTokenIndex - 1];
    private bool IsAtEnd() => _activeTokenIndex >= tokenizedFile.Tokens.Count || Peek().Kind == TokenKind.Eof;
    private Token Consume(TokenKind kind, string error) => ActiveTokenMatch(kind) ? Advance() : throw new ByronHighLevelParserException(error, _activeTokenIndex > 0 ? Previous().Span : Peek().Span);
    private SourceSpan ExpandSpan(Token firstToken, Token endToken) => ExpandSpan(firstToken.Span, endToken.Span);
    private SourceSpan ExpandSpan(AstNode node, Token endToken) => ExpandSpan(node.Span, endToken.Span);
    private SourceSpan ExpandSpan(Token firstToken, AstNode endNode) => ExpandSpan(firstToken.Span, endNode.Span);
    private SourceSpan ExpandSpan(SourceSpan start, SourceSpan end) => start with { End = end.End };
}
