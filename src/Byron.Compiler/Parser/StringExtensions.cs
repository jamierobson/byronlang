namespace Byron.Compiler.Parser;

public static class StringExtensions
{
    extension(string value)
    {
        public string Mangle() => value.Replace(".", "$$");
    }
}