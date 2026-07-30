using System.Collections;

namespace Byron.Compiler.AST;

public static class CanonicalNames
{
    public static string InModule(IEnumerable<string> modulePath, string shortName) => $"{string.Join('.', modulePath)}.{shortName}')";
}