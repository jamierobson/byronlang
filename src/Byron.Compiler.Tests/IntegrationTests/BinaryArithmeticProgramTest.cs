namespace Byron.Compiler.Tests.IntegrationTests;

public class BinaryArithmeticProgramTest : ProgramSnapshotTestBase
{
    protected override string Program() => "fn main(): i32 { return (1 + 3 * 3 - 2) * 2.0; }";
}