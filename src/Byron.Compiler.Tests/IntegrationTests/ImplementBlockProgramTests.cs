using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace Byron.Compiler.Tests.IntegrationTests;

public class DefinedImplementBlockProgramTests: ProgramSnapshotTestBase
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

public class InvokeImplementBlockProgramTests : ProgramSnapshotTestBase
{
    protected override string Program() => """
                                           struct Point1d { x: i32 }
                                           implement Point1d {
                                             fn getValue(self: &Self): i32 {
                                               return self.*.x;
                                             }
                                           }

                                           fn main(): i32 {
                                               let point = Point1d {x: 5};
                                               var a = Point1d.getValue(&point);
                                               var b = point.getValue();
                                               return a;
                                           }
                                           """;
}