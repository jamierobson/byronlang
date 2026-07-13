namespace Byron.Compiler.Lexer;

public class Tokenizer
{
    private readonly string _source;
    private int _position;
    private int _line;
    private int _firstCharacterOffset;

    public Tokenizer(string source)
    {
        _source = source;
        _position = 0;
        _line = 1;
        _firstCharacterOffset = 0;
    }

    public List<Token> Tokenise()
    {
        var tokens = new List<Token>();

        while (!ReachedEndOfFile())
        {
            SkipWhitespace();
            if (ReachedEndOfFile())
            {
                break;
            }

            var token = NextToken();
            tokens.Add(token);
        }

        tokens.Add(Token.Create(TokenKind.Eof, "", CreateSourceSpan(_position, _position)));
        return tokens;
    }

    private Token NextToken()
    {
        var initialPosition = _position;
        var character = Peek();

        if (char.IsLetter(character) || character == '_')
        {
            return ScanWord(initialPosition);
        }

        if (char.IsDigit(character))
        {
            return ScanNumber(initialPosition);
        }

        if (character == '"')
        {
            return ScanString(initialPosition);
        }

        if (character == '\'')
        {
            return ScanChar(initialPosition);
        }
        
        // if (character == '`')
        // {
        //     return ScanSingleChar(TokenKind.Backtick, initialPosition);
        // }

        // Everything else is punctuation / operators
        return ScanPunctuation(initialPosition);
    }

    private Token ScanWord(int start)
    {
        while (!ReachedEndOfFile() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            Consume();
        }

        var lexeme = _source[start.._position];
        var kind = Keywords.GetOrAssumeIdentifier(lexeme);
        return Token.Create(kind, lexeme, CreateSourceSpan(start, _position));
    }

    private Token ScanNumber(int start)
    {
        // Base prefix
        if (Peek() == '0' && _position + 1 < _source.Length)
        {
            var next = _source[_position + 1];
            switch (next)
            {
                case 'x' or 'X':
                    return ScanHex(start);
                case 'b' or 'B':
                    return ScanBinary(start);
                case 'o' or 'O':
                    return ScanOctal(start);
            }
        }

        // Decimal integer or float
        while (!ReachedEndOfFile() && (char.IsDigit(Peek()) || Peek() == '_'))
        {
            Consume();
        }

        // Float detection
        if (!ReachedEndOfFile() && Peek() == '.' && _position + 1 < _source.Length && char.IsDigit(_source[_position + 1]))
        {
            Consume(); // .
            while (!ReachedEndOfFile() && (char.IsDigit(Peek()) || Peek() == '_'))
            {
                Consume();
            }

            // Exponent
            if (!ReachedEndOfFile() && (Peek() == 'e' || Peek() == 'E'))
            {
                Consume();
                if (!ReachedEndOfFile() && (Peek() == '+' || Peek() == '-'))
                {
                    Consume();
                }
                
                while (!ReachedEndOfFile() && char.IsDigit(Peek()))
                {
                    Consume();
                }
            }

            var floatLexeme = _source[start.._position].Replace("_", "");
            if (double.TryParse(floatLexeme, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
            {
                return Token.CreateWithValue(TokenKind.FloatLiteral, _source[start.._position], doubleValue, CreateSourceSpan(start, _position));
            }

            return Token.Error(_source[start.._position], "Malformed float literal", CreateSourceSpan(start, _position));
        }

        var intLexeme = _source[start.._position].Replace("_", "");
        if (long.TryParse(intLexeme, out var integerValue))
        {
            return Token.CreateWithValue(TokenKind.IntLiteral, _source[start.._position], integerValue, CreateSourceSpan(start, _position));
        }

        return Token.Error(_source[start.._position], "Malformed integer literal", CreateSourceSpan(start, _position));
    }

    private Token ScanHex(int start)
    {
        Consume(); Consume(); // 0x
        var digitStart = _position;
        while (!ReachedEndOfFile() && (IsHexDigit(Peek()) || Peek() == '_')) Consume();

        var digits = _source[digitStart.._position].Replace("_", "");
        if (digits.Length == 0)
        {
            return Token.Error(_source[start.._position], "Empty hex literal", CreateSourceSpan(start, _position));
        }

        if (Convert.ToInt64(digits, 16) is var longValue)
        {
            return Token.CreateWithValue(TokenKind.IntLiteral, _source[start.._position], longValue, CreateSourceSpan(start, _position));
        }

        return Token.Error(_source[start.._position], "Malformed hex literal", CreateSourceSpan(start, _position));
    }

    private Token ScanBinary(int start)
    {
        Consume(); Consume(); // 0b
        var digitStart = _position;
        while (!ReachedEndOfFile() && (Peek() == '0' || Peek() == '1' || Peek() == '_')) Consume();

        var digits = _source[digitStart.._position].Replace("_", "");
        if (digits.Length == 0)
        {
            return Token.Error(_source[start.._position], "Empty binary literal", CreateSourceSpan(start, _position));
        }

        try
        {
            var binaryValue = Convert.ToInt64(digits, 2);
            return Token.CreateWithValue(TokenKind.IntLiteral, _source[start.._position], binaryValue, CreateSourceSpan(start, _position));
        }
        catch
        {
            return Token.Error(_source[start.._position], "Malformed binary literal", CreateSourceSpan(start, _position));
        }
    }

    private Token ScanOctal(int start)
    {
        Consume(); Consume(); // 0o
        var digitStart = _position;
        while (!ReachedEndOfFile() && ((Peek() >= '0' && Peek() <= '7') || Peek() == '_'))
        {
            Consume();
        }

        var digits = _source[digitStart.._position].Replace("_", "");
        if (digits.Length == 0)
        {
            return Token.Error(_source[start.._position], "Empty octal literal", CreateSourceSpan(start, _position));
        }

        try
        {
            var octalValue = Convert.ToInt64(digits, 8);
            return Token.CreateWithValue(TokenKind.IntLiteral, _source[start.._position], octalValue, CreateSourceSpan(start, _position));
        }
        catch
        {
            return Token.Error(_source[start.._position], "Malformed octal literal", CreateSourceSpan(start, _position));
        }
    }

    private Token ScanString(int start)
    {
        Consume(); // Opening "
        var value = new System.Text.StringBuilder();

        while (!ReachedEndOfFile() && Peek() != '"')
        {
            if (Peek() == '\n')
            {
                // Unterminated string
                return Token.Error(_source[start.._position], "Unterminated string literal", CreateSourceSpan(start, _position));
            }

            if (Peek() == '\\')
            {
                Consume(); // backslash
                var escapeCharacter = ReachedEndOfFile() ? '\0' : Consume();
                value.Append(escapeCharacter switch
                {
                    'n'  => '\n',
                    't'  => '\t',
                    'r'  => '\r',
                    '\\' => '\\',
                    '"'  => '"',
                    '0'  => '\0',
                    _    => escapeCharacter   // unknown escape — pass through, semantic pass can warn
                });
            }
            else
            {
                value.Append(Consume());
            }
        }

        if (ReachedEndOfFile())
        {
            return Token.Error(_source[start.._position], "Unterminated string literal", CreateSourceSpan(start, _position));
        }

        Consume(); // Closing "
        return Token.CreateWithValue(TokenKind.StringLiteral, _source[start.._position], value.ToString(), CreateSourceSpan(start, _position));
    }

    private Token ScanChar(int start)
    {
        Consume(); // Opening '

        if (ReachedEndOfFile() || Peek() == '\n')
        {
            return Token.Error(_source[start.._position], "Unterminated char literal", CreateSourceSpan(start, _position));
        }

        char value;
        if (Peek() == '\\')
        {
            Consume();
            var escapeCharacter = ReachedEndOfFile() ? '\0' : Consume();
            value = escapeCharacter switch
            {
                'n'  => '\n',
                't'  => '\t',
                'r'  => '\r',
                '\\' => '\\',
                '\'' => '\'',
                '0'  => '\0',
                _    => escapeCharacter
            };
        }
        else
        {
            value = Consume();
        }

        if (ReachedEndOfFile() || Peek() != '\'')
        {
            return Token.Error(_source[start.._position], "Unterminated rune literal (expected closing ')", CreateSourceSpan(start, _position));
        }

        Consume(); // Closing '
        return Token.CreateWithValue(TokenKind.RuneLiteral, _source[start.._position], value, CreateSourceSpan(start, _position));
    }

    private Token ScanPunctuation(int start)
    {
        SourceSpan PunctuationSpan() => CreateSourceSpan(start, _position);
        
        var character = Consume();

        return character switch
        {
            '{' => Token.Create(TokenKind.LBrace,    "{", PunctuationSpan()),
            '}' => Token.Create(TokenKind.RBrace,    "}", PunctuationSpan()),
            '(' => Token.Create(TokenKind.LParen,    "(", PunctuationSpan()),
            ')' => Token.Create(TokenKind.RParen,    ")", PunctuationSpan()),
            '[' => Token.Create(TokenKind.LBracket,  "[", PunctuationSpan()),
            ']' => Token.Create(TokenKind.RBracket,  "]", PunctuationSpan()),
            '<' => ConsumingMatch('=') 
                ? Token.Create(TokenKind.LessEquals, "<=", PunctuationSpan())
                : ConsumingMatch('<')
                    ? Token.Create(TokenKind.LAngleLAngle, "<<", PunctuationSpan())
                    : Token.Create(TokenKind.LAngle, "<", PunctuationSpan()),
            '>' => ConsumingMatch('=') 
                ? Token.Create(TokenKind.GreaterEquals, ">=", PunctuationSpan())
                : ConsumingMatch('>')
                    ? Token.Create(TokenKind.RAngleRAngle,">>", PunctuationSpan())
                    : Token.Create(TokenKind.RAngle,">", PunctuationSpan()),
            ',' => Token.Create(TokenKind.Comma, ",", PunctuationSpan()),
            ';' => Token.Create(TokenKind.Semicolon, ";", PunctuationSpan()),
            '|' => ConsumingMatch('|') 
                ? Token.Create(TokenKind.PipePipe, "||", PunctuationSpan())
                : Token.Create(TokenKind.Pipe, "|", PunctuationSpan()),
            '&' => ConsumingMatch('&') 
                ? Token.Create(TokenKind.AmpersandAmpersand, "&&", PunctuationSpan())
                : Token.Create(TokenKind.Ampersand, "&", PunctuationSpan()),
            ':' => ConsumingMatch(':') 
                ? Token.Create(TokenKind.ColonColon, "::", PunctuationSpan())
                : Token.Create(TokenKind.Colon, ":", PunctuationSpan()),
            '.' => ConsumingMatch('.') 
                ? ConsumingMatch('=') 
                    ? Token.Create(TokenKind.DotDotEquals, "..=", PunctuationSpan())
                    : Token.Create(TokenKind.DotDot, "..", PunctuationSpan())
                    : Token.Create(TokenKind.Dot, ".", PunctuationSpan()),
            '=' => ConsumingMatch('=') 
                ? Token.Create(TokenKind.EqualsEquals, "==", PunctuationSpan())
                : ConsumingMatch('>') 
                        ? Token.Create(TokenKind.FatArrow, "=>", PunctuationSpan())
                        : Token.Create(TokenKind.Equals, "=", PunctuationSpan()),
            '!' => ConsumingMatch('=') 
                ? Token.Create(TokenKind.BangEquals, "!=", PunctuationSpan())
                : Token.Create(TokenKind.Bang, "!", PunctuationSpan()),
            '+' => ConsumingMatch('=') 
                ? Token.Create(TokenKind.PlusEquals, "+=", PunctuationSpan())
                : Token.Create(TokenKind.Plus, "+", PunctuationSpan()),
            '-' => ConsumingMatch('=') 
                ? Token.Create(TokenKind.MinusEquals, "-=", PunctuationSpan())
                : ConsumingMatch('>') 
                    ? Token.Create(TokenKind.Arrow, "->", PunctuationSpan())
                    : Token.Create(TokenKind.Minus, "-", PunctuationSpan()),
            '*' => ConsumingMatch('=') 
                ? Token.Create(TokenKind.StarEquals, "*=", PunctuationSpan())
                : Token.Create(TokenKind.Asterisk, "*", PunctuationSpan()),
            '/' => ConsumingMatch('=') 
                ? Token.Create(TokenKind.SlashEquals, "/=", PunctuationSpan())
                : ConsumingMatch('/') 
                    ? ScanLineComment(start)
                    : ConsumingMatch('*') 
                        ? ScanBlockComment(start)
                        : Token.Create(TokenKind.Slash, "/", PunctuationSpan()),
            '^' => Token.Create(TokenKind.Caret, "^", PunctuationSpan()),
            '?' => Token.Create(TokenKind.QuestionMark, "?", PunctuationSpan()),
            '@' => Token.Create(TokenKind.At, "@", PunctuationSpan()),
            '#' => Token.Create(TokenKind.Hash, "#", PunctuationSpan()),
            '$' => Token.Create(TokenKind.Dollar, "$", PunctuationSpan()),
            '%' => Token.Create(TokenKind.Percent, "%", PunctuationSpan()),
            '\\' => Token.Create(TokenKind.Backslash, "\\", PunctuationSpan()),
            '_' => Token.Create(TokenKind.Underscore, "_", PunctuationSpan()),
            '`' => Token.Create(TokenKind.Backtick, "`", PunctuationSpan()),
            _ => Token.Error(character.ToString(), $"Unexpected character '{character}'", CreateSourceSpan(start, _position))
        };
    }

    private Token ScanLineComment(int start)
    {
        // The comment starter // has already been skipped, so we are now checking for a document comment ///
        var isDocumentComment = !ReachedEndOfFile() && Peek() == '/';

        while (!ReachedEndOfFile() && Peek() != '\n')
        {
            Consume();
        }

        var kind = isDocumentComment ? TokenKind.DocComment : TokenKind.LineComment;
        var lexeme = _source[start.._position];
        return Token.Create(kind, lexeme, CreateSourceSpan(start, _position));
    }

    private Token ScanBlockComment(int start)
    {
        // The comment starter /* has already been skipped, so we are now checking for a document comment ///
        int depth = 1; // nested /* /* */ */

        while (!ReachedEndOfFile() && depth > 0)
        {
            if (Peek() == '/' && _position + 1 < _source.Length && _source[_position + 1] == '*')
            {
                Consume(); Consume();
                depth++;
            }
            else if (Peek() == '*' && _position + 1 < _source.Length && _source[_position + 1] == '/')
            {
                Consume(); Consume();
                depth--;
            }
            else
            {
                if (Peek() == '\n') TrackNewline();
                Consume();
            }
        }

        if (depth > 0)
        {
            return Token.Error(_source[start.._position], "Unterminated block comment", CreateSourceSpan(start, _position));
        }

        return Token.Create(TokenKind.BlockComment, _source[start.._position], CreateSourceSpan(start, _position));
    }

    private void SkipWhitespace()
    {
        while (!ReachedEndOfFile())
        {
            var character = Peek();
            if (character == '\n')
            {
                Consume();
                TrackNewline();
            }
            else if (char.IsWhiteSpace(character))
            {
                Consume();
            }
            else
            {
                break;
            }
        }
    }

    private void TrackNewline()
    {
        _line++;
        _firstCharacterOffset = _position;
    }

    private bool ReachedEndOfFile() => _position >= _source.Length;

    private char Peek() => _source[_position];

    private char Consume()
    {
        var character = _source[_position++];
        return character;
    }

    private bool ConsumingMatch(char expected)
    {
        if (ReachedEndOfFile() || _source[_position] != expected)
        {
            return false;
        }
        _position++;
        return true;
    }

    private SourceSpan CreateSourceSpan(int start, int end)
    {
        var col = start - _firstCharacterOffset + 1;
        return new SourceSpan(_line, col, start, end);
    }

    private static bool IsHexDigit(char character)
        => char.IsDigit(character)
        || character is >= 'a' and <= 'f'
        || character is >= 'A' and <= 'F';
}
