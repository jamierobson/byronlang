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
    In,
    Match,
    Return,
    Yield,
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
    Type,
    Typeof,

    // Self
    Self,
    CapitalSelf,

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
    ColonColon,     // ::
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
    
    RAngleRAngle,   // >>
    LAngleLAngle,   // <<

    Ampersand,      // &
    AmpersandAmpersand,         // &&
    PipePipe,       // ||

    Plus,           // +
    PlusEquals,     // +=
    Minus,          // -
    MinusEquals,    // -=
    Asterisk,           // *
    StarEquals,     // *=
    Slash,          // /
    SlashEquals,    // /=
    Caret,          // ^

    QuestionMark,   // ?

    // -------------------------------------------------------------------------
    // Future Reserved Symbols
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
    LexError,
}
