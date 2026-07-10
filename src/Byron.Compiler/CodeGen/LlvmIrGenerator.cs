using System;
using System.Linq;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.CodeGen;

public class LlvmIrGenerator
{
    private readonly GeneratorContext _ctx = new();

    public string Generate(ProgramNode program)
    {
        foreach (var declaration in program.Declarations)
        {
            GenerateTopLevelDeclaration(declaration);
        }
        return _ctx.GetGeneratedIr();
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
        _ctx.ResetRegisters();
        
        string returnType = MapType(node.ReturnType);
        
        // Map parameters: e.g., "i32 %0, i64 %1"
        var paramsJoined = string.Join(", ", node.Parameters.Select((p, i) => $"{MapType(p.Type)} %{i}"));

        _ctx.EmitLine($"define {returnType} @{node.Name}({paramsJoined}) {{");
        
        GenerateBlockStatement(node.Body);
        
        // Safety fallback if a void/unit function misses an explicit return
        if (node.ReturnType is VoidTypeNode or UnitTypeNode)
        {
            _ctx.EmitLine("    ret void");
        }
        
        _ctx.EmitLine("}\n");
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
            // Future statements (VariableDeclarationNode, YieldStatementNode, etc.) plug directly into here
            default:
                throw new NotImplementedException($"Statement {node.GetType().Name} is not implemented.");
        }
    }

    // --- ONE-AND-DONE HANDLERS ---

    private void GenerateReturnStatement(ReturnStatementNode node)
    {
        if (node.Expression == null)
        {
            _ctx.EmitLine("    ret void");
            return;
        }

        // Evaluate the expression to find its output register/value and type
        var (value, typeStr) = GenerateExpression(node.Expression);
        _ctx.EmitLine($"    ret {typeStr} {value}");
    }

    private (string Value, string TypeStr) GenerateExpression(ExpressionNode node)
    {
        return node switch
        {
            IntegerLiteralNode lit => (lit.Value.ToString(), "i32"), // Defaulting to i32 for now
            VariableExpressionNode varExpr => (varExpr.Name, "i32"), // Quick placeholder
            _ => throw new NotImplementedException($"Expression {node.GetType().Name} is not implemented.")
        };
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