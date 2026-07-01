namespace Byron.Compiler.Lexer;

public enum TokenKind
{
    Identifier,
    IntLiteral,
    FloatLiteral,
    StringLiteral,
    RuneLiteral,

    Pub,
    Fn,
    Struct,
    Trait,
    Enum,
    Union,
    Implement,

    If,
    Else,
    While,
    For,
    Match,
    Return,
    Continue,
    Break,

    Let,
    Var,
    Give,
    Take,

    Defer,
    ErrorDefer,
    OnError,

    // Types
    Void,
    Unit,
    Type,
    Typeof,

    // Self
    Self,               // self
    CapitalSelf,        // Self

    Import,
    Use,
    Using,
    As,

    Unsafe,
    Untracked,

    Dynamic,

    Async,
    Await,

    Comptime,

    And,
    Or,

    // -------------------------------------------------------------------------
    // Reserved Identifiers
    // These are known names, not grammar keywords.
    // Parser / semantic pass gives them meaning.
    // -------------------------------------------------------------------------
    True,
    False,
    Ok,
    Some,
    None,
    Error,

    LBrace,         // {
    RBrace,         // }
    LParen,         // (
    RParen,         // )
    LBracket,       // [
    RBracket,       // ]
    LAngle,         // <
    RAngle,         // >

    Dot,            // .
    Comma,          // ,
    Colon,          // :
    DoubleColon,    // ::
    Semicolon,      // ;
    Pipe,           // |

    DotDot,         // ..
    DotDotEquals,   // ..=

    Equals,         // =
    EqualsEquals,   // ==
    Bang,           // !
    BangEquals,     // !=
    LessEquals,     // <=
    GreaterEquals,  // >=

    Ampersand,      // &
    AmpAmp,         // &&
    PipePipe,       // ||

    Plus,           // +
    PlusEquals,     // +=
    Minus,          // -
    MinusEquals,    // -=
    Star,           // *
    StarEquals,     // *=
    Slash,          // /
    SlashEquals,    // /=
    Caret,          // ^

    QuestionMark,   // ?

    // -------------------------------------------------------------------------
    // Future Reserved Symbols
    // Lexed and emitted; parser will reject them for now.
    // -------------------------------------------------------------------------
    At,             // @
    Hash,           // #
    Dollar,         // $
    Percent,        // %
    Backslash,      // \
    Underscore,     // _
    Arrow,          // ->
    FatArrow,       // =>
    Backtick,       // `

    // -------------------------------------------------------------------------
    // Trivia
    // -------------------------------------------------------------------------
    LineComment,    // //
    DocComment,     // ///
    BlockComment,   // /* */

    // -------------------------------------------------------------------------
    // Meta
    // -------------------------------------------------------------------------
    Eof,
    LexError,       // unrecognised character
}
