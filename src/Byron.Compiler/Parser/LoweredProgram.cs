using Byron.Compiler.SemanticAnalysis;

namespace Byron.Compiler.Parser;

public class LoweredProgram(
    AST.LowLevel.ProgramNode program,
    Dictionary<AST.HighLevel.TypeNode, AST.LowLevel.TypeNode> highToLowLevelTypeMap,
    TypeMap highLevelExpressionTypeMap)
{
    public AST.LowLevel.ProgramNode Program { get; } = program;

    public AST.LowLevel.TypeNode GetType(AST.LowLevel.ExpressionNode expressionNode)
    {
        return highToLowLevelTypeMap[
            highLevelExpressionTypeMap.GetType((AST.HighLevel.ExpressionNode)expressionNode.SourceNode)
        ];
    }
}