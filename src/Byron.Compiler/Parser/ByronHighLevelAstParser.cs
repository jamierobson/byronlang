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
                    functions.Add(ParseFunctionDeclaration());
                    break;
                case TokenKind.Struct:
                    structs.Add(ParseStructDeclaration());
                    break;
                default:
                    throw new ByronNotImplementedException(token.Kind.ToString(), this, token.Span);
            }   
        }

        return new ProgramNode([..functions, ..structs]);
    }

    private StructDeclarationNode ParseStructDeclaration()
    {
        var structToken = Consume(TokenKind.Struct, "Expected 'struct'.");
        var nameToken = Consume(TokenKind.Identifier, "Expected struct name.");

        var fields = ParseStructFields();
        
        return new StructDeclarationNode(nameToken.Lexeme, [], fields, structToken.Span with { End = Peek().Span.End} );
    }

    private List<StructFieldNode> ParseStructFields()
    {
        var fields = new List<StructFieldNode>();
        _ = Consume(TokenKind.LBrace, "Expected '{'.");

        while (!ActiveTokenMatch(TokenKind.RBrace))
        {
            var name = Consume(TokenKind.Identifier, "Expected field name");
            _ = Consume(TokenKind.Colon, "Expected ':'.");
            var type = ParseTypeSignature();

            fields.Add(new StructFieldNode(name.Lexeme, type, name.Span with { End = type.Span.End }));

            if (ActiveTokenMatch(TokenKind.RBrace))
            {
                break;
            }
            _ = Consume(TokenKind.Comma, "Expected ',' separator between field declarations.");
        }
        
        Advance();
        return fields;
    }

    public FunctionDeclarationNode ParseFunctionDeclaration()
    {
        var fnToken = Consume(TokenKind.Fn, "Expected 'fn'.");
        var nameToken = Consume(TokenKind.Identifier, "Expected function name.");

        var parameters = ParseFunctionArguments(); 
        _ = Consume(TokenKind.Colon, "Expected ':'.");
        var returnType = ParseTypeSignature();
        var body = ParseBlockStatement();

        return new FunctionDeclarationNode(nameToken.Lexeme, [], parameters, returnType, body, new SourceSpan(fnToken.Span.Line, fnToken.Span.Column, fnToken.Span.Start, body.Span.End));
    }

    public List<ParameterNode> ParseFunctionArguments()
    {   
        _ = Consume(TokenKind.LParen, "Expected '('.");
        var parameters = new List<ParameterNode>();
        if (!ActiveTokenMatch(TokenKind.RParen))
        {
            do
            {
                var parameterName = Consume(TokenKind.Identifier, "Expected parameter name.");
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
                
                var parameterType = ParseTypeSignature();
                parameters.Add(new ParameterNode(receiverBindingOwnership, parameterName.Lexeme, parameterType, parameterName.Span with {End = parameterType.Span.End}));
            } while (ConsumingActiveTokenMatch(TokenKind.Comma));
        }

        _ = Consume(TokenKind.RParen, "Expected ')'.");
        return parameters;
    }

    private TypeNode ParseTypeSignature()
    {
        if (ConsumingActiveTokenMatch(TokenKind.Ampersand))
        {
            var ampersand = Previous();   
            var isMutable = ConsumingActiveTokenMatch(TokenKind.Var);
            var targetType = ParseTypeSignature();
            return new ReferenceTypeNode(targetType, isMutable, ampersand.Span with {End = targetType.Span.End});
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
    
    private NominalTypeNode ParseNominalTypeNode(Token firstIdentifier)
    {
        var path = new List<string> { firstIdentifier.Lexeme };
        var startSpan = firstIdentifier.Span;
        var endSpan = firstIdentifier.Span;

        while (ConsumingActiveTokenMatch(TokenKind.Dot))
        {
            var segment = Consume(TokenKind.Identifier, "Expected identifier after '.' in type path.");
            path.Add(segment.Lexeme);
            endSpan = segment.Span;
        }

        var name = path[^1];
        path.RemoveAt(path.Count - 1);

        return new NominalTypeNode(name, path, startSpan with { End = endSpan.End });
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
}