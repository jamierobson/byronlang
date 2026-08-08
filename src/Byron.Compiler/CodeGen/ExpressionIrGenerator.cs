using System.Globalization;
using Byron.Compiler.AST;
using Byron.Compiler.AST.LowLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.CodeGen;

public partial class LlvmIrGenerator
{
    private LlvmType IntegerType(long value) => value < int.MinValue || value > int.MaxValue
        ? new LlvmType.Int(64)
        : new LlvmType.Int(32);
    
    
    private LlvmType FloatType(double value) => value < float.MinValue || value > float.MaxValue
        ? new LlvmType.Float(64)
        : new LlvmType.Float(32);
    
    private (string ReturnValue, LlvmType ReturnType) GenerateExpression(ExpressionNode node)
    {
        return node switch
        {
            IntegerLiteralNode intLiteral => (intLiteral.Value.ToString(), IntegerType(intLiteral.Value)),
            FloatLiteralNode floatLiteral => (floatLiteral.Value.ToString(CultureInfo.InvariantCulture), FloatType(floatLiteral.Value)),
            BoolLiteralNode boolean => (boolean.Value ? "1" : "0", new LlvmType.Boolean()),
            VariableExpressionNode variable => GenerateVariableLoad(variable),
            BinaryExpressionNode binary => GenerateBinaryExpression(binary),
            CallExpressionNode call => GenerateCallExpression(call),
            
            StructFieldInitializationExpressionNode fieldInitialization => GenerateStructFieldInitializationExpression(fieldInitialization),
            MemberAccessExpressionNode memberAccess => GenerateMemberAccessExpression(memberAccess),
            
            _ => throw new ByronNotImplementedException(node.GetType(), this)
        };
    }
    
    private (string ReturnValue, LlvmType ReturnType) GenerateStructFieldInitializationExpression(StructFieldInitializationExpressionNode node)
    {
        var layout = _context.GetStructLayout(node.StructName);
        var structType = new LlvmType.Struct(node.StructName);

        var structPointerRegister = _context.AllocateRegister();
        _context.EmitLine($"    {structPointerRegister} = alloca {structType}");

        foreach (var fieldInitializer in node.FieldInitializers)
        {
            var (fieldValue, fieldType) = GenerateExpression(fieldInitializer.Value);
            var fieldIndex = layout.GetFieldIndex(fieldInitializer.FieldName);
            var fieldPointerRegister = _context.AllocateRegister();
            _context.EmitLine($"    {fieldPointerRegister} = getelementptr {structType}, {structType}* {structPointerRegister}, i32 0, i32 {fieldIndex}");
            _context.EmitLine($"    store {fieldType} {fieldValue}, {fieldType}* {fieldPointerRegister}");
        }
        
        var valueRegister = _context.AllocateRegister();
        _context.EmitLine($"    {valueRegister} = load {structType}, {structType}* {structPointerRegister}");
        return (valueRegister, structType);
    }
    
    private (string ReturnValue, LlvmType ReturnType) GenerateMemberAccessExpression(MemberAccessExpressionNode node)
    {
        var (fieldPointerRegister, fieldType) = GenerateMemberAccessPointer(node);
        var loadedValueRegister = _context.AllocateRegister();
        _context.EmitLine($"    {loadedValueRegister} = load {fieldType}, {fieldType}* {fieldPointerRegister}");

        return (loadedValueRegister, fieldType);
    }
    
    private (string ReturnValue, LlvmType ReturnType) GenerateCallExpression(CallExpressionNode node)
    {
        if (node.Callee is not VariableExpressionNode functionIdentifier)
        {
            throw new ByronNotImplementedException("Dynamic function pointers/closures", this);
        }

        var evaluatedArguments = node.Arguments.Select(GenerateExpression).ToList();
        var argumentIr = string.Join(", ", evaluatedArguments.Select(arg => $"{arg.ReturnType} {arg.ReturnValue}"));

        var llvmType = _context.GetFunctionReturnType(functionIdentifier.Name);

        if (llvmType is LlvmType.Void)
        {
            _context.EmitLine($"    call void @{functionIdentifier.Name}({argumentIr})");
            return ("void", new LlvmType.Void());
        }
        else
        {
            var resultRegister = _context.AllocateRegister();
            _context.EmitLine($"    {resultRegister} = call {llvmType} @{functionIdentifier.Name}({argumentIr})");
            return (resultRegister, llvmType);
        }
    }
    
    private (string ReturnValue, LlvmType ReturnType) GenerateVariableLoad(VariableExpressionNode node)
    {
        var stackPointer = _context.LookupVariable(node.Name);
        var register = _context.AllocateRegister();

        var llvmType = stackPointer.LlvmType;
        
        _context.EmitLine($"    {register} = load {llvmType}, {llvmType}* {stackPointer.Pointer}");
        return (register, llvmType);
    }
    
    private (string ReturnValue, LlvmType ReturnType) GenerateBinaryExpression(BinaryExpressionNode node)
    {
        var (leftValue, leftLlvmType) = GenerateExpression(node.Left);
        var (rightValue, rightLlvmType) = GenerateExpression(node.Right);

        if (leftLlvmType != rightLlvmType)
        {
            
            // if (CanPromoteToType(leftLlvmType, rightLlvmType))
            // {
            //     (leftValue, leftLlvmType) = EmitPromotion(leftValue, leftLlvmType, rightLlvmType);
            // }
            // else if (CanPromoteToType(rightLlvmType, leftLlvmType))
            // {
            //     (rightValue, rightLlvmType) = EmitPromotion(rightValue, rightLlvmType, leftLlvmType);
            // }
            // else
            // {
                throw new ByronCodeGenerationException($"Type mismatch in binary expression: {leftLlvmType} and {rightLlvmType}");
            // }
        }

        var isFloat = leftLlvmType is LlvmType.Float;
        var isUnsigned = leftLlvmType is LlvmType.UnsignedInt;

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
                returnType = new LlvmType.Boolean();
                var booleanInstruction = BooleanOperationInstruction(node.Operator, isFloat, isUnsigned);
                _context.EmitLine($"    {resultRegister} = {typeComparisonInstruction} {booleanInstruction} {leftLlvmType} {leftValue}, {rightValue}");
                break;
            default:
                throw new ByronNotImplementedException($"LLVM IR mapping for operator {node.Operator}", this);
        };

        return (resultRegister, returnType);
    }

    // todo: This belongs inn semantic analysis
    // private bool CanPromoteToType(LlvmType promotionCandidate, LlvmType targetType)
    // {
    //     if (promotionCandidate is LlvmType.Int && targetType is LlvmType.Float)
    //     {
    //         return true;
    //     }
    //
    //     if (promotionCandidate is LlvmType.Int sourceInt && targetType is LlvmType.Int targetInt)
    //     {
    //         return sourceInt.BitWidth <  targetInt.BitWidth;
    //     }
    //
    //     if (promotionCandidate is LlvmType.Float sourceFloat && targetType is LlvmType.Float targetFloat)
    //     {
    //         return sourceFloat.BitWidth < targetFloat.BitWidth;
    //     }
    //
    //     return false;
    // }

    //todo: This belongs in lowering pass
    // private (string ReturnValue, LlvmType ReturnType) EmitPromotion(
    //     string value,
    //     LlvmType promotingType,
    //     LlvmType targetType
    //     )
    // {
    //     if (promotingType == targetType)
    //     {
    //         return (value, promotingType);
    //     }
    //
    //     var resultRegister = _context.AllocateRegister();
    //
    //     if (promotingType is LlvmType.Int && targetType is LlvmType.Float)
    //     {
    //         _context.EmitLine($"{resultRegister} = sitofp {promotingType} {value} to {targetType}");
    //         return (resultRegister, targetType);
    //     }
    //     
    //     if (promotingType is LlvmType.Int sourceInt && targetType is LlvmType.Int targetInt && sourceInt.BitWidth < targetInt.BitWidth)
    //     {
    //         _context.EmitLine($"{resultRegister} = sext {promotingType} {value} to {targetType}");
    //         return (resultRegister, targetType);
    //     }
    //     
    //     if (promotingType is LlvmType.Float sourceFloat && targetType is LlvmType.Float targetFloat && sourceFloat.BitWidth < targetFloat.BitWidth)
    //     {
    //         _context.EmitLine($"{resultRegister} = fpext {promotingType} {value} to {targetType}");
    //         return (resultRegister, targetType);
    //     }
    //
    //     throw new ByronNotImplementedException($"promotion of {promotingType} to {targetType}", this);
    // }

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
}