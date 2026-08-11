using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace Byron.Compiler.Tests.IntegrationTests;

public class SimpleImplementBlockProgramTests: ProgramSnapshotTestBase
{
    protected override string Program() => """
                                           struct Point1d { x: i32 }
                                           implement Point1d {
                                             fn getValue(self: &Self): i32 {
                                               return self.*.x;
                                             }
                                           }

                                           fn main(): i32 {
                                               return 0;
                                           }
                                           """;
}