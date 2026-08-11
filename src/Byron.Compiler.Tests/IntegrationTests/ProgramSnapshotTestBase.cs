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
        var ast = new ByronHighLevelAstParser(tokenStream).Parse();
        var semanticAnalysisResult = new SemanticAnalysisDriver(ast).Analyze();
        var loweredAst = new ByronLoweringPass(semanticAnalysisResult).Lower();
        var llvmIr = new LlvmIrGenerator(loweredAst).Generate();
        
        return Verify(llvmIr);
    }
}