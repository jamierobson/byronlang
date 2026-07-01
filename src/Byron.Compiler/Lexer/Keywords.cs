namespace Byron.Compiler.Lexer;

/// <summary>
/// Maps source text to token kinds for all keywords and reserved identifiers.
/// </summary>
public static class Keywords
{
    private static readonly Dictionary<string, TokenKind> TokenKindMap = new()
    {
        // --- Declarations ---
        ["pub"]         = TokenKind.Pub,
        ["fn"]          = TokenKind.Fn,
        ["struct"]      = TokenKind.Struct,
        ["trait"]       = TokenKind.Trait,
        ["enum"]        = TokenKind.Enum,
        ["union"]       = TokenKind.Union,
        ["implement"]   = TokenKind.Implement,

        // --- Control flow ---
        ["if"]          = TokenKind.If,
        ["else"]        = TokenKind.Else,
        ["while"]       = TokenKind.While,
        ["for"]         = TokenKind.For,
        ["match"]       = TokenKind.Match,
        ["return"]      = TokenKind.Return,
        ["continue"]    = TokenKind.Continue,
        ["break"]       = TokenKind.Break,

        // --- Bindings & ownership ---
        ["let"]         = TokenKind.Let,
        ["var"]         = TokenKind.Var,
        ["give"]        = TokenKind.Give,
        ["take"]        = TokenKind.Take,

        // --- Error handling ---
        ["defer"]       = TokenKind.Defer,
        ["errordefer"]  = TokenKind.ErrorDefer,
        ["onerror"]     = TokenKind.OnError,

        // --- Types ---
        ["void"]        = TokenKind.Void,
        ["Unit"]        = TokenKind.Unit,
        ["type"]        = TokenKind.Type,
        ["typeof"]      = TokenKind.Typeof,

        // --- Self ---
        ["self"]        = TokenKind.Self,
        ["Self"]        = TokenKind.CapitalSelf,

        // --- Import / scope ---
        ["import"]      = TokenKind.Import,
        ["use"]         = TokenKind.Use,
        ["using"]       = TokenKind.Using,
        ["as"]          = TokenKind.As,

        // --- Safety ---
        ["unsafe"]      = TokenKind.Unsafe,
        ["untracked"]   = TokenKind.Untracked,

        // --- Dynamic Dispatch ---
        ["dynamic"]     = TokenKind.Dynamic,

        // --- Async (reserved) ---
        ["async"]       = TokenKind.Async,
        ["await"]       = TokenKind.Await,

        // --- Comptime ---
        ["comptime"]    = TokenKind.Comptime,

        // --- Reserved word-operators ---
        ["and"]         = TokenKind.And,
        ["or"]          = TokenKind.Or,

        // --- Reserved identifiers ---
        ["true"]        = TokenKind.True,
        ["false"]       = TokenKind.False,
        ["Ok"]          = TokenKind.Ok,
        ["Some"]        = TokenKind.Some,
        ["None"]        = TokenKind.None,
        ["Error"]       = TokenKind.Error,
    };

    // todo: Don't have this be an out, prefer (bool, value)
    public static bool TryGet(string text, out TokenKind kind)
        => TokenKindMap.TryGetValue(text, out kind);

    public static TokenKind GetOrIdentifier(string text)
        => TokenKindMap.TryGetValue(text, out var kind) ? kind : TokenKind.Identifier;
}
