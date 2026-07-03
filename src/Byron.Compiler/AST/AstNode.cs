using Byron.Compiler.Lexer;

namespace Byron.Compiler.AST;

// =============================================================================
// Base
// =============================================================================

public abstract record AstNode(SourceSpan Span);

// =============================================================================
// Patterns — used in if let / if var / match
// =============================================================================

public abstract record Pattern(SourceSpan Span) : AstNode(Span);

/// <summary>
/// Some(x) or Some(x: T) — binds inner value to name
/// IsMutable: true for var Some(x), false for let Some(x)
/// BindingName: the name to bind to (null if not binding)
/// BindingType: optional type annotation
/// </summary>
public record SomePattern(
    bool IsMutable,
    string? BindingName,
    TypeNode? BindingType,
    SourceSpan Span) : Pattern(Span);

/// <summary>None — no binding possible</summary>
public record NonePattern(SourceSpan Span) : Pattern(Span);

/// <summary>
/// Ok(x) or Ok(x: T) — binds inner value to name
/// IsMutable: true for var Ok(x), false for let Ok(x)
/// BindingName: the name to bind to
/// BindingType: optional type annotation
/// </summary>
public record OkPattern(
    bool IsMutable,
    string? BindingName,
    TypeNode? BindingType,
    SourceSpan Span) : Pattern(Span);

/// <summary>
/// Error(e) or Error(e: T) — binds inner error to name
/// IsMutable: true for var Error(e), false for let Error(e)
/// BindingName: the name to bind to
/// BindingType: optional type annotation
/// </summary>
public record ErrorPattern(
    bool IsMutable,
    string? BindingName,
    TypeNode? BindingType,
    SourceSpan Span) : Pattern(Span);

/// <summary>_ — explicit discard, no binding</summary>
public record DiscardPattern(SourceSpan Span) : Pattern(Span);

// =============================================================================
// Types
// =============================================================================

public abstract record TypeNode(SourceSpan Span) : AstNode(Span);

/// <summary>A named type — i32, bool, MyStruct, etc.</summary>
public record NamedType(string Name, SourceSpan Span) : TypeNode(Span);

/// <summary>A pointer type — *i32</summary>
public record PointerType(TypeNode Inner, SourceSpan Span) : TypeNode(Span);

/// <summary>An array type — i32[], optional fixed size</summary>
public record ArrayType(TypeNode Inner, int? Size, SourceSpan Span) : TypeNode(Span);

/// <summary>void return type</summary>
public record VoidType(SourceSpan Span) : TypeNode(Span);

/// <summary>A generic type — Result&lt;T, E&gt;, Option&lt;T&gt;, Owned&lt;T&gt;</summary>
public record GenericType(string Name, IReadOnlyList<TypeNode> TypeArguments, SourceSpan Span) : TypeNode(Span);