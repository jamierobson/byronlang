using Byron.Compiler.SemanticAnalysis;

namespace Byron.Compiler.Parser;

public class LoweredProgram
{
    private readonly TypeMap _highLevelExpressionTypeMap;
    private readonly Dictionary<AST.HighLevel.TypeNode, AST.LowLevel.TypeNode> _highToLowLevelTypeMap;
    public AST.LowLevel.ProgramNode Program { get; }
    
    public LoweredProgram(AST.LowLevel.ProgramNode program, Dictionary<AST.HighLevel.TypeNode, AST.LowLevel.TypeNode> highToLowLevelTypeMap,TypeMap highLevelExpressionTypeMap)
    {
        Program = program;
        _highToLowLevelTypeMap = highToLowLevelTypeMap;
        _highLevelExpressionTypeMap = highLevelExpressionTypeMap;
    }

    public AST.LowLevel.TypeNode GetType(AST.LowLevel.ExpressionNode expressionNode)
    {
        return _highToLowLevelTypeMap[
            _highLevelExpressionTypeMap.GetType(expressionNode.SourceNode)
        ];
    }
}