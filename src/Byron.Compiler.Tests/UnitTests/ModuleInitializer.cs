using System.Runtime.CompilerServices;

namespace Byron.Compiler.Tests.UnitTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.UseSourceFileRelativeDirectory("Snapshots");
    }
}