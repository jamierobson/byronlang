namespace Byron.Compiler.Tests.IntegrationTests;

public class WhileLoopProgramTest : ProgramSnapshotTestBase
{
    protected override string Program() =>
        """
        fn main(): i32 { 
            var i = 5;
            var sum = 0;
            
            while(i != 0) {
                sum = sum + i;
                i = i - 1;
            }
            
            return sum;
        }
        """;
}