using Byron.Compiler.CodeGen;
using Byron.Compiler.Lexer;
using Byron.Compiler.Parser;
using Byron.Compiler.SemanticAnalysis;

namespace Byron.Compiler.Tests.IntegrationTests;

public abstract class ProgramSnapshotTestBase
{
    protected abstract string Program();

    [Fact]
    public Task VerifyIr()
    {
        var program = Program();
        var tokenStream = new Tokenizer(program).Tokenise();
        var tokenFile = new TokenizedFile("test", tokenStream);
        var ast = new ByronHighLevelAstParser(tokenFile).Parse();
        var semanticAnalysisResult = new SemanticAnalysisDriver(ast).Analyze();
        var lowered = new ByronLoweringPass(semanticAnalysisResult).Lower();
        var llvmIr = new LlvmIrGenerator(lowered).Generate();
        
        return Verify(llvmIr);
    }
}