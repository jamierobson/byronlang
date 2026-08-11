namespace Byron.Compiler.Tests.IntegrationTests;

public class TrivialProgramTest : ProgramSnapshotTestBase
{
    protected override string Program() => "fn main(): i32 { return 0; }";
}