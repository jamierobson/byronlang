using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.CodeGen;

public class LlvmIrGenerator
{
    private readonly GeneratorContext _context = new();

    public string Generate(ProgramNode program)
    {
        foreach (var declaration in program.Declarations)
        {
            GenerateTopLevelDeclaration(declaration);
        }
        return _context.GetGeneratedIr();
    }

    private void GenerateTopLevelDeclaration(TopLevelDeclarationNode node)
    {
        switch (node)
        {
            case FunctionDeclarationNode func:
                GenerateFunctionDeclaration(func);
                break;
            default:
                throw new NotImplementedException($"Top-level node {node.GetType().Name} is not implemented.");
        }
    }

    private void GenerateFunctionDeclaration(FunctionDeclarationNode node)
    {
        _context.ResetRegisters();
        
        var returnType = MapType(node.ReturnType);
        
        var functionParameterIr = string.Join(", ", node.Parameters.Select((parameterNode, i) => $"{MapType(parameterNode.Type)} %{i}"));

        _context.EmitLine($"define {returnType} @{node.Name}({functionParameterIr}) {{");
        
        GenerateBlockStatement(node.Body);
        
        // Add a return when reaching the end of a void function
        if (node.ReturnType is VoidTypeNode or UnitTypeNode)
        {
            _context.EmitLine("    ret void");
        }
        
        _context.EmitLine("}\n");
    }

    private void GenerateBlockStatement(BlockStatementNode node)
    {
        foreach (var statement in node.Statements)
        {
            GenerateStatement(statement);
        }
    }

    private void GenerateStatement(StatementNode node)
    {
        switch (node)
        {
            case ReturnStatementNode ret:
                GenerateReturnStatement(ret);
                break;
            default:
                throw new NotImplementedException($"Statement {node.GetType().Name} is not implemented.");
        }
    }

    private void GenerateReturnStatement(ReturnStatementNode node)
    {
        if (node.Expression == null)
        {
            _context.EmitLine("    ret void");
            return;
        }

        var (returnValue, returnType) = GenerateExpression(node.Expression);
        _context.EmitLine($"    ret {returnType} {returnValue}");
    }

    private (string ReturnValue, string ReturnType) GenerateExpression(ExpressionNode node)
    {
        return node switch
        {
            IntegerLiteralNode literal => (literal.Value.ToString(), "i32"), // Defaulting to i32 for now
            VariableExpressionNode variable => (variable.Name, "i32"), // Quick placeholder
            BinaryExpressionNode binary => GenerateBinaryExpression(binary),
            _ => throw new NotImplementedException($"Expression {node.GetType().Name} is not implemented.")
        };
    }
    
    private (string ReturnValue, string ReturnType) GenerateBinaryExpression(BinaryExpressionNode node)
    {
        var (leftValue, leftType) = GenerateExpression(node.Left);
        var (rightValue, rightType) = GenerateExpression(node.Right);

        if (leftType != rightType)
        {
            throw new Exception($"Type mismatch in binary expression: {leftType} and {rightType}");
        }

        var llvmInstruction = node.Operator switch
        {
            BinaryOperator.Add => "add",
            BinaryOperator.Subtract => "sub",
            BinaryOperator.Multiply => "mul",
            BinaryOperator.Divide => "sdiv",
            _ => throw new NotImplementedException($"LLVM IR mapping for operator {node.Operator} is not implemented.")
        };

        var resultRegister = _context.AllocateRegister();

        _context.EmitLine($"    {resultRegister} = {llvmInstruction} {leftType} {leftValue}, {rightValue}");

        return (resultRegister, leftType);
    }

    private string MapType(TypeNode node)
    {
        return node switch
        {
            VoidTypeNode => "void",
            UnitTypeNode => "void",
            Int32TypeNode => "i32",
            Int64TypeNode => "i64",
            ReferenceTypeNode r => $"{MapType(r.Target)}*",
            _ => throw new NotImplementedException($"Type mapping for {node.GetType().Name} is not implemented.")
        };
    }
}