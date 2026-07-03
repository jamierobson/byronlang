using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST;

// =============================================================================
// Expressions — produce a value
// =============================================================================

public abstract record Expression(SourceSpan Span) : AstNode(Span);

// -----------------------------------------------------------------------------
// Literals
// -----------------------------------------------------------------------------

public record LiteralExpression(object Value, LiteralKind Kind, SourceSpan Span)
    : Expression(Span);

public enum LiteralKind
{
    Int,
    Float,
    String,
    Bool,
    Rune,
}

// -----------------------------------------------------------------------------
// Identifier
// -----------------------------------------------------------------------------

/// <summary>
/// A bare name in expression position. The semantic pass resolves
/// what declaration this refers to.
/// </summary>
public record IdentifierExpression(string Name, SourceSpan Span)
    : Expression(Span);

// -----------------------------------------------------------------------------
// Binary and Unary
// -----------------------------------------------------------------------------

public record BinaryExpression(
    Expression  Left,
    TokenKind   Operator,
    Expression  Right,
    SourceSpan  Span) : Expression(Span);

public record UnaryExpression(
    TokenKind   Operator,
    Expression  Operand,
    SourceSpan  Span) : Expression(Span);

// -----------------------------------------------------------------------------
// Calls and Member access
// -----------------------------------------------------------------------------

/// <summary>
/// foo(a, b, c)
/// Callee is an Expression to support foo.bar() and foo.bar.baz()
/// </summary>
public record CallExpression(
    Expression                  Callee,
    IReadOnlyList<Expression>   Arguments,
    SourceSpan                  Span) : Expression(Span);

/// <summary>foo.bar</summary>
public record MemberExpression(
    Expression  Target,
    string      Member,
    SourceSpan  Span) : Expression(Span);

/// <summary>foo[i]</summary>
public record IndexExpression(
    Expression  Target,
    Expression  Index,
    SourceSpan  Span) : Expression(Span);

// -----------------------------------------------------------------------------
// Error handling expressions
// -----------------------------------------------------------------------------

/// <summary>
/// expr?
/// Sugar for: onerror error { return error; }
/// Propagates error up to the caller.
/// </summary>
public record PropagateExpression(
    Expression  Operand,
    SourceSpan  Span) : Expression(Span);

/// <summary>
/// expr onerror fallback
/// Sugar for: onerror { yield fallback; }
/// Provides a fallback value if the operand is an error.
/// Fallback is either an Expression (simple) or a Block (explicit handler).
/// Block form must yield or return.
/// </summary>
public record OnErrorExpression(
    Expression          Operand,
    OnErrorFallback     Fallback,
    SourceSpan          Span) : Expression(Span);

public abstract record OnErrorFallback(SourceSpan Span) : AstNode(Span);

/// <summary>onerror 5 — simple expression fallback</summary>
public record ExpressionFallback(Expression Value, SourceSpan Span)
    : OnErrorFallback(Span);

/// <summary>onerror error { ... } — block fallback with error binding</summary>
public record BlockFallback(string? ErrorBinding, BlockStatement Body, SourceSpan Span)
    : OnErrorFallback(Span);

/// <summary>onerror _ — explicit discard of error</summary>
public record DiscardFallback(SourceSpan Span)
    : OnErrorFallback(Span);

// -----------------------------------------------------------------------------
// Match expressions
// -----------------------------------------------------------------------------

/// <summary>
/// match scrutinee {
///     let Some(x) => { ... }
///     let Error(e) => { ... }
/// }
/// Each arm has its own pattern and body.
/// </summary>
public record MatchExpression(
    Expression                  Scrutinee,
    IReadOnlyList<MatchArm>     Arms,
    SourceSpan                  Span) : Expression(Span);

/// <summary>
/// A single arm in a match expression.
/// let Some(x) => body
/// var Error(e: ErrorType) => body
/// </summary>
public record MatchArm(
    Pattern         MatchPattern,
    BlockStatement  Body,
    SourceSpan      Span) : AstNode(Span);