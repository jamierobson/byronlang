using System.Runtime.CompilerServices;

namespace Valuator.IntegrationTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.UseSourceFileRelativeDirectory("Snapshots");
    }
}