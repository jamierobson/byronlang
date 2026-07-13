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

    private void GenerateStatement(StatementNode node)
    {
        switch (node)
        {
            case ReturnStatementNode statement:
                GenerateReturnStatement(statement);
                break;
            case VariableDeclarationNode declaration:
                GenerateVariableDeclaration(declaration);
                break;
            default:
                throw new NotImplementedException($"Statement {node.GetType().Name} is not implemented.");
        }
    }

    private (string ReturnValue, string ReturnType) GenerateExpression(ExpressionNode node)
    {
        return node switch
        {
            IntegerLiteralNode literal => (literal.Value.ToString(), "i32"), // Defaulting to i32 for now
            VariableExpressionNode variable => GenerateVariableLoad(variable),
            BinaryExpressionNode binary => GenerateBinaryExpression(binary),
            CallExpressionNode call => GenerateCallExpression(call),
            _ => throw new NotImplementedException($"Expression {node.GetType().Name} is not implemented.")
        };
    }
    
    private (string ReturnValue, string ReturnType) GenerateCallExpression(CallExpressionNode node)
    {
        if (node.Callee is not VariableExpressionNode functionIdentifier)
        {
            throw new NotImplementedException("Dynamic function pointers/closures are not implemented yet.");
        }

        var evaluatedArguments = node.Arguments.Select(GenerateExpression).ToList();
        var argumentIr = string.Join(", ", evaluatedArguments.Select(arg => $"{arg.ReturnType} {arg.ReturnValue}"));

        // For now, look up or default the return type (assume i32 if matching typical declarations)
        var llvmType = "i32"; 

        if (llvmType == "void")
        {
            _context.EmitLine($"    call void @{functionIdentifier.Name}({argumentIr})");
            return ("void", "void");
        }
        else
        {
            var resultRegister = _context.AllocateRegister();
            _context.EmitLine($"    {resultRegister} = call {llvmType} @{functionIdentifier.Name}({argumentIr})");
            return (resultRegister, llvmType);
        }
    }

    private void GenerateFunctionDeclaration(FunctionDeclarationNode node)
    {
        _context.ResetRegisters();
        
        var returnType = MapType(node.ReturnType);
        
        var functionParameterIr = string.Join(", ", node.Parameters.Select((parameterNode, i) => $"{MapType(parameterNode.Type)} %arg_{i}"));

        _context.EmitLine($"define {returnType} @{node.Name}({functionParameterIr}) {{");

        MoveArgumentsOnToStackFrame(node);
        
        GenerateBlockStatement(node.Body);
        
        // Add a return when reaching the end of a void function
        if (node.ReturnType is VoidTypeNode or UnitTypeNode)
        {
            _context.EmitLine("    ret void");
        }
        
        _context.EmitLine("}\n");
    }

    private void MoveArgumentsOnToStackFrame(FunctionDeclarationNode node)
    {
        for (var i = 0; i < node.Parameters.Count; i++)
        {
            var param = node.Parameters[i];
            var stackPointer = $"%{param.Name}.addr";
            var typeStr = MapType(param.Type);
        
            _context.EmitLine($"    {stackPointer} = alloca {typeStr}");
        
            _context.EmitLine($"    store {typeStr} %arg_{i}, {typeStr}* {stackPointer}");
        
            _context.DeclareVariable(param.Name, stackPointer);
        }
    }

    private void GenerateBlockStatement(BlockStatementNode node)
    {
        foreach (var statement in node.Statements)
        {
            GenerateStatement(statement);
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
    
    private void GenerateVariableDeclaration(VariableDeclarationNode node)
    {
        var (variableValue, variableType) = GenerateExpression(node.Initializer);

        var stackPointer = $"%{node.Name}.addr";
        _context.DeclareVariable(node.Name, stackPointer);
        _context.EmitLine($"    {stackPointer} = alloca {variableType}");
        _context.EmitLine($"    store {variableType} {variableValue}, {variableType}* {stackPointer}");
    }
    
    private (string ReturnValue, string ReturnType) GenerateVariableLoad(VariableExpressionNode node)
    {
        var stackPointer = _context.LookupVariable(node.Name);
        var register = _context.AllocateRegister();

        var llvmType = "i32"; // default for now since we're only testing with numerics
        
        _context.EmitLine($"    {register} = load {llvmType}, {llvmType}* {stackPointer}");
        return (register, llvmType);
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