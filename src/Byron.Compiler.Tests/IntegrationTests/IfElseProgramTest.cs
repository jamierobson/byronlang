namespace Byron.Compiler.Tests.IntegrationTests;

public class IfProgramTest : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        fn main(): i32 { 
           if(5 == 5) {
               return 5;
           } 
           return 0;
        }
        """;
}

public class IfElseProgramTest : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        fn main(): i32 { 
           if(5 == 5) {
               return 5;
           } else {
               return 0;
           }
        }
        """;
}