namespace Byron.Compiler.AST;

public static class CanonicalNames
{
    public static string InModule(IList<string> modulePath, string shortName) => modulePath.Any() ? $"{string.Join('.', modulePath)}.{shortName}')" : shortName;
}