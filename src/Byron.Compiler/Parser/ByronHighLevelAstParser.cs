using Byron.Compiler.AST;
using Byron.Compiler.Lexer;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.Parser;

public partial class ByronHighLevelAstParser
{
    private readonly List<Token> _tokens;
    private int _activeTokenIndex = 0;

    public ByronHighLevelAstParser(List<Token> tokens) => _tokens = tokens;

    public ProgramNode Parse()
    {
        var functions = new List<FunctionDeclarationNode>();
        while (!IsAtEnd())
        {
            functions.Add(ParseFunctionDeclaration());
        }

        return new ProgramNode([..functions]);
    }

    public FunctionDeclarationNode ParseFunctionDeclaration()
    {
        var fnToken = Consume(TokenKind.Fn, "Expected 'fn'.");
        var nameToken = Consume(TokenKind.Identifier, "Expected function name.");

        var parameters = ParseFunctionArguments(); 
        _ = Consume(TokenKind.Colon, "Expected ':'.");
        var returnType = ParseTypeSignature();
        var body = ParseBlockStatement();

        return new FunctionDeclarationNode(nameToken.Lexeme, parameters, returnType, body, new SourceSpan(fnToken.Span.Line, fnToken.Span.Column, fnToken.Span.Start, body.Span.End));
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
                    throw new ByronParserException(Peek());
                }
                
                var parameterType = ParseTypeSignature();
                parameters.Add(new ParameterNode(receiverBindingOwnership, parameterName.Lexeme, parameterType, new SourceSpan(parameterName.Span.Line, parameterName.Span.Column, parameterName.Span.Start, parameterType.Span.End)));
            } while (ConsumingActiveTokenMatch(TokenKind.Comma));
        }

        _ = Consume(TokenKind.RParen, "Expected ')'.");
        return parameters;
    }

    private TypeNode ParseTypeSignature()
    {
        var token = Advance();
        return token.Lexeme switch
        {
            "boolean" => new BoolTypeNode(token.Span),
            "i8" => new Int8TypeNode(token.Span),
            "i16" => new Int16TypeNode(token.Span),
            "i32" => new Int32TypeNode(token.Span),
            "i64" => new Int64TypeNode(token.Span),
            "u8" => new UInt8TypeNode(token.Span),
            "u16" => new UInt16TypeNode(token.Span),
            "u32" => new UInt32TypeNode(token.Span),
            "u64" => new UInt64TypeNode(token.Span),
            "f32" => new Float32TypeNode(token.Span),
            "f64" => new Float64TypeNode(token.Span),
            "void" => new VoidTypeNode(token.Span),
            "unit" => new UnitTypeNode(token.Span),
            "rune" => new RuneTypeNode(token.Span),
            _ => throw new ByronParserException($"Unknown type signature target: {token.Lexeme}", token.Span)
        };
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _activeTokenIndex++;
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
    private Token Peek() => _tokens[_activeTokenIndex];
    private Token Previous() => _tokens[_activeTokenIndex - 1];
    private Token PeekNext() => _tokens[_activeTokenIndex + 1];
    private bool IsAtEnd() => _activeTokenIndex >= _tokens.Count || Peek().Kind == TokenKind.Eof;
    private Token Consume(TokenKind kind, string error) => ActiveTokenMatch(kind) ? Advance() : throw new ByronParserException(error, SourceSpan.Empty);
}