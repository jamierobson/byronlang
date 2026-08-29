namespace Byron.Compiler.Lexer;

public record TokenizedFile(string FilePath, List<Token> Tokens);