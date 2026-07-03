using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST;

// =============================================================================
// Statements — do work, never produce a value unless they contain yield
// =============================================================================

public abstract record Statement(SourceSpan Span) : AstNode(Span);

// -----------------------------------------------------------------------------
// Block
// -----------------------------------------------------------------------------

/// <summary>
/// { statement* }
/// A block without yield is a statement.
/// A block with yield is an expression — produces exactly one value.
/// </summary>
public record BlockStatement(
    IReadOnlyList<Statement>    Body,
    SourceSpan                  Span) : Statement(Span);

// -----------------------------------------------------------------------------
// Bindings
// -----------------------------------------------------------------------------

/// <summary>
/// let x: i32 = expr;
/// Immutable binding.
/// Type is nullable — will be inferred when type inference is implemented.
/// Initialiser is nullable — uninitialised let pending decision.
/// </summary>
public record LetStatement(
    string      Name,
    TypeNode?   Type,
    Expression? Initialiser,
    SourceSpan  Span) : Statement(Span);

/// <summary>
/// var x: i32 = expr;
/// Mutable binding. Same shape as LetStatement, different semantics.
/// </summary>
public record VarStatement(
    string      Name,
    TypeNode?   Type,
    Expression? Initialiser,
    SourceSpan  Span) : Statement(Span);

// -----------------------------------------------------------------------------
// Control flow
// -----------------------------------------------------------------------------

/// <summary>
/// return expr;
/// return;
/// Always exits the containing function. No exceptions.
/// </summary>
public record ReturnStatement(
    Expression? Value,
    SourceSpan  Span) : Statement(Span);

/// <summary>
/// yield expr;
/// Fulfils the nearest binding. Only valid inside an expression block context.
/// </summary>
public record YieldStatement(
    Expression  Value,
    SourceSpan  Span) : Statement(Span);

/// <summary>
/// if condition { ... }
/// if condition { ... } else { ... }
/// Always a statement. Becomes an expression only when blocks yield.
/// </summary>
public record IfStatement(
    Expression      Condition,
    BlockStatement  Then,
    BlockStatement? Else,
    SourceSpan      Span) : Statement(Span);

/// <summary>
/// if let Pattern(x) = expr { ... } else { ... }
/// Pattern match with immutable binding.
/// Binding scoped to Then block only.
/// </summary>
public record IfLetStatement(
    Pattern         MatchPattern,
    Expression      Value,
    BlockStatement  Then,
    BlockStatement? Else,
    SourceSpan      Span) : Statement(Span);

/// <summary>
/// if var Pattern(x) = expr { ... } else { ... }
/// Pattern match with mutable binding.
/// Binding scoped to Then block only.
/// </summary>
public record IfVarStatement(
    Pattern         MatchPattern,
    Expression      Value,
    BlockStatement  Then,
    BlockStatement? Else,
    SourceSpan      Span) : Statement(Span);

/// <summary>
/// if None = expr { ... }
/// Pattern check with no binding. No let or var.
/// </summary>
public record IfPatternStatement(
    Pattern         MatchPattern,
    Expression      Value,
    BlockStatement  Then,
    BlockStatement? Else,
    SourceSpan      Span) : Statement(Span);

/// <summary>
/// while condition { ... }
/// Always a statement.
/// </summary>
public record WhileStatement(
    Expression      Condition,
    BlockStatement  Body,
    SourceSpan      Span) : Statement(Span);

/// <summary>
/// for — not yet designed, reserved as a node stub
/// </summary>
public record ForStatement(SourceSpan Span) : Statement(Span);

/// <summary>
/// A bare expression used as a statement.
/// e.g. a function call where the return value is explicitly discarded.
/// The obligation tracker enforces that non-void returns are handled.
/// </summary>
public record ExpressionStatement(
    Expression  Expression,
    SourceSpan  Span) : Statement(Span);

/// <summary>
/// let _ = expr; or _ = expr;
/// Explicit discard — satisfies the obligation tracker.
/// </summary>
public record DiscardStatement(
    Expression  Expression,
    SourceSpan  Span) : Statement(Span);

/// <summary>
/// defer { ... }
/// Executes block on scope exit. Lowering not yet designed.
/// </summary>
public record DeferStatement(
    BlockStatement  Body,
    SourceSpan      Span) : Statement(Span);

/// <summary>
/// errdefer { ... }
/// Executes block on scope exit via error path. Lowering not yet designed.
/// </summary>
public record ErrDeferStatement(
    BlockStatement  Body,
    SourceSpan      Span) : Statement(Span);

/// <summary>break; — exits the nearest loop</summary>
public record BreakStatement(SourceSpan Span) : Statement(Span);

/// <summary>continue; — skips to next iteration of nearest loop</summary>
public record ContinueStatement(SourceSpan Span) : Statement(Span);
