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
            case IfStatementNode @if:
                GenerateIfStatement(@if);
                break;
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
            IntegerLiteralNode literal => (literal.Value.ToString(), "i32"),
            BoolLiteralNode boolean => (boolean.Value ? "1" : "0", "i1"),
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

    private string ArithmeticOperationInstruction(BinaryOperator nodeOperator, bool isFloat, bool isUnsigned)
    {
        return nodeOperator switch
        {
            BinaryOperator.Add => isFloat ? "fadd" : "add",
            BinaryOperator.Subtract => isFloat ? "fsub" : "sub",
            BinaryOperator.Multiply => isFloat ? "fmul" : "mul",
            BinaryOperator.Divide => isFloat ? "fdiv" : (isUnsigned ? "udiv" : "sdiv"),
            _ => throw new InvalidOperationException($"Operation {nodeOperator} is not an arithmetic operation")
        };
    }

    private string BooleanOperationInstruction(BinaryOperator nodeOperator, bool isFloat, bool isUnsigned)
    {
        return nodeOperator switch
        {
            BinaryOperator.Equal => isFloat ? "oeq" : "eq",
            BinaryOperator.NotEqual => isFloat ? "one" : "ne",
            
            BinaryOperator.LessThan => isFloat ? "olt" : (isUnsigned ? "ult" : "slt"),
            BinaryOperator.LessThanOrEqual => isFloat ? "ole" : (isUnsigned ? "ule" : "sle"),
            
            BinaryOperator.GreaterThan => isFloat ? "ogt" : (isUnsigned ? "ugt" : "sgt"),
            BinaryOperator.GreaterThanOrEqual => isFloat ? "oge" : (isUnsigned ? "uge" : "sge"),
            
            _ => throw new InvalidOperationException($"Operation {nodeOperator} is not a boolean operation")
        };
    }
    
    private (string ReturnValue, string ReturnType) GenerateBinaryExpression(BinaryExpressionNode node)
    {
        var (leftValue, leftLlvmType) = GenerateExpression(node.Left);
        var (rightValue, rightLlvmType) = GenerateExpression(node.Right);

        if (leftLlvmType != rightLlvmType)
        {
            throw new Exception($"Type mismatch in binary expression: {leftLlvmType} and {rightLlvmType}");
        }

        var isFloat = leftLlvmType is "float" or "double";
        var isUnsigned = IsUnsignedLlvmType(leftLlvmType);

        var resultRegister = _context.AllocateRegister();
        var returnType = leftLlvmType;
        
        switch(node.Operator)
        {
            case BinaryOperator.Add:
            case BinaryOperator.Subtract:
            case BinaryOperator.Multiply: 
            case BinaryOperator.Divide:
                var arithmeticOperation = ArithmeticOperationInstruction(node.Operator, isFloat, isUnsigned); 
                _context.EmitLine($"    {resultRegister} = {arithmeticOperation} {leftLlvmType} {leftValue}, {rightValue}");
                break;
            case BinaryOperator.Equal:
            case BinaryOperator.NotEqual:
            case BinaryOperator.LessThan:
            case BinaryOperator.LessThanOrEqual:
            case BinaryOperator.GreaterThan:
            case BinaryOperator.GreaterThanOrEqual:
                var typeComparisonInstruction = isFloat ? "fcmp" : "icmp";
                returnType = "i1";
                var booleanInstruction = BooleanOperationInstruction(node.Operator, isFloat, isUnsigned);
                _context.EmitLine($"    {resultRegister} = {typeComparisonInstruction} {booleanInstruction} {leftLlvmType} {leftValue}, {rightValue}");
                break;
            default:
                throw new NotImplementedException($"LLVM IR mapping for operator {node.Operator} is not implemented.");
        };

        return (resultRegister, returnType);
    }
    
    private void GenerateIfStatement(IfStatementNode node)
    {
        var (condValue, condType) = GenerateExpression(node.Condition);
        if (condType != "i1")
        {
            throw new Exception($"If condition must be a boolean (i1), but got {condType}");
        }

        var branchId = _context.AllocateLabelId(); // Assuming your context has a counter helper
        var thenLabel = $"if_then_{branchId}";
        var elseLabel = $"if_else_{branchId}";
        var mergeLabel = $"if_merge_{branchId}";

        var falsePathLabel = node is IfElseStatementNode ? elseLabel : mergeLabel;

        _context.EmitLine($"    br i1 {condValue}, label %{thenLabel}, label %{falsePathLabel}");

        _context.EmitLine($"\n{thenLabel}:");
        GenerateBlockStatement(node.ThenBranch);
        
        var thenTerminates = BlockEndsWithTerminator(node.ThenBranch);
        
        if (!thenTerminates)
        {
            _context.EmitLine($"    br label %{mergeLabel}");
        }

        bool elseTerminates;
        if (node is IfElseStatementNode ifElseStatementNode)
        {
            _context.EmitLine($"\n{elseLabel}:");
            GenerateBlockStatement(ifElseStatementNode.ElseBranch);

            elseTerminates = BlockEndsWithTerminator(ifElseStatementNode.ElseBranch); 
            if (!elseTerminates)
            {
                _context.EmitLine($"    br label %{mergeLabel}");
            }
        }
        else
        {
            elseTerminates = false;
        }

        if (!thenTerminates || !elseTerminates)
        {
            _context.EmitLine($"\n{mergeLabel}:");
        }
    }

private static bool BlockEndsWithTerminator(BlockStatementNode block)
{
    if (block.Statements.Count == 0) return false;
    var last = block.Statements[^1];
    return last is ReturnStatementNode; // Todo: extend with break/continue/yield
}

    private static bool IsUnsignedLlvmType(string llvmType) => llvmType.StartsWith('u');

    private static string MapType(TypeNode node)
    {
        return node switch
        {
            VoidTypeNode => "void",
            UnitTypeNode => "void",
            
            Int8TypeNode => "i8",
            Int16TypeNode => "i16",
            Int32TypeNode => "i32",
            Int64TypeNode => "i64",
        
            UInt8TypeNode => "i8",
            UInt16TypeNode => "i16",
            UInt32TypeNode => "i32",
            UInt64TypeNode => "i64",
        
            Float32TypeNode => "float",
            Float64TypeNode => "double",
        
            BoolTypeNode => "i1",
            RuneTypeNode => "i32",
        
            ReferenceTypeNode r => $"{MapType(r.Target)}*",
            _ => throw new NotImplementedException($"Type mapping for {node.GetType().Name} is not implemented.")
        };
    }
}