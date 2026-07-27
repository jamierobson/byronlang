using Byron.Compiler.AST;
using Byron.Compiler.AST.LowLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.CodeGen;

public partial class LlvmIrGenerator
{
    private const string TemporaryDefaultReturnType = "i32";
    
    private (string ReturnValue, string ReturnType) GenerateExpression(ExpressionNode node)
    {
        return node switch
        {
            IntegerLiteralNode literal => (literal.Value.ToString(), "i32"),
            BoolLiteralNode boolean => (boolean.Value ? "1" : "0", "i1"),
            VariableExpressionNode variable => GenerateVariableLoad(variable),
            BinaryExpressionNode binary => GenerateBinaryExpression(binary),
            CallExpressionNode call => GenerateCallExpression(call),
            _ => throw new ByronNotImplementedException(node.GetType(), this)
        };
    }
    
    private (string ReturnValue, string ReturnType) GenerateCallExpression(CallExpressionNode node)
    {
        if (node.Callee is not VariableExpressionNode functionIdentifier)
        {
            throw new ByronNotImplementedException("Dynamic function pointers/closures", this);
        }

        var evaluatedArguments = node.Arguments.Select(GenerateExpression).ToList();
        var argumentIr = string.Join(", ", evaluatedArguments.Select(arg => $"{arg.ReturnType} {arg.ReturnValue}"));

        var llvmType = TemporaryDefaultReturnType; 

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
    
    private (string ReturnValue, string ReturnType) GenerateVariableLoad(VariableExpressionNode node)
    {
        var stackPointer = _context.LookupVariable(node.Name);
        var register = _context.AllocateRegister();

        var llvmType = TemporaryDefaultReturnType;
        
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
            throw new ByronCodeGenerationException($"Type mismatch in binary expression: {leftLlvmType} and {rightLlvmType}");
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
                throw new ByronNotImplementedException($"LLVM IR mapping for operator {node.Operator}", this);
        };

        return (resultRegister, returnType);
    }
}