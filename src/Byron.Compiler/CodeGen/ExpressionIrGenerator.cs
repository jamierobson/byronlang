using System.Diagnostics;
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
            CastFloatToIntNode floatToInt => GenerateCastFloatToInt(floatToInt),
            CastIntToFloatNode intToFloat => GenerateCastIntToFloat(intToFloat),
            ExtendIntegerNode extendInt => GenerateExtendInteger(extendInt),
            ExtendFloatNode extendFloat => GenerateExtendFloat(extendFloat),
            DereferenceExpressionNode dereference => GenerateDereference(dereference),
            AddressOfExpressionNode addressOf => GenerateAddressOf(addressOf),
            
            StructFieldInitializationExpressionNode fieldInitialization => GenerateStructFieldInitializationExpression(fieldInitialization),
            MemberAccessExpressionNode memberAccess => GenerateMemberAccessExpression(memberAccess),
            
            _ => throw new ByronNotImplementedException(node.GetType(), this, node.SourceNode.Span)
        };
    }

    private (string ReturnValue, LlvmType ReturnType) AddressOf(VariableExpressionNode variable)
    {
        var symbolAddress = _context.LookupVariable(variable.Name);
        if (symbolAddress.LlvmType is LlvmType.Pointer pointerType)
        {
            var ptrReg = _context.AllocateRegister();
            _context.EmitLine($"    {ptrReg} = load {pointerType}, {pointerType}* {symbolAddress.Pointer}");
            return (ptrReg, pointerType);
        }

        return (symbolAddress.Pointer.ToString(), new LlvmType.Pointer(symbolAddress.LlvmType));
    }

    private (string ReturnValue, LlvmType ReturnType) AddressOf(MemberAccessExpressionNode memberAccess)
    {
        var (fieldPtrRegister, fieldType) = GenerateMemberAccessPointer(memberAccess);
        return (fieldPtrRegister, new LlvmType.Pointer(fieldType));
    }
    
    private (string ReturnValue, LlvmType ReturnType) AddressOf(DereferenceExpressionNode dereference)
    {
        var (valueRegister, valueType) = GenerateExpression(dereference.Target);
        if (valueType is LlvmType.Pointer ptrType)
        {
            return (valueRegister, ptrType);
        }

        throw new ByronCodeGenerationException($"Cannot dereference non-pointer type '{valueType}'.");
    }
    
    private (string ReturnValue, LlvmType ReturnType) GenerateAddressOf(AddressOfExpressionNode addressOf)
    {
        var target = addressOf.Target;

        return addressOf.Target switch
        {
            VariableExpressionNode variable => AddressOf(variable),
            MemberAccessExpressionNode memberAccess => AddressOf(memberAccess),
            DereferenceExpressionNode dereference => AddressOf(dereference),
            _ => throw new ByronCodeGenerationException($"Cannot take address of expression of type '{target.GetType().Name}'.")
        };
    }

    private (string ReturnValue, LlvmType ReturnType) GenerateDereference(DereferenceExpressionNode dereference)
    {
        var (targetPointerValue, targetPointerType) = GenerateExpression(dereference.Target);
        var valueTypeSymbol = program.GetType(dereference);
        var llvmValueType = LlvmType.From(valueTypeSymbol);
        
        var resultRegister = _context.AllocateRegister();
        _context.EmitLine($"    {resultRegister} = load {llvmValueType}, {targetPointerType} {targetPointerValue}");
        return (resultRegister, llvmValueType);
    }

    private (string ReturnValue, LlvmType ReturnType) GenerateCastFloatToInt(CastFloatToIntNode floatToInt)
    {
        var (operandValue, operandType) = GenerateExpression(floatToInt.Operand);
        var targetLlvmType = new LlvmType.Int(floatToInt.TargetType.BitWidth);
        
        var resultRegister = _context.AllocateRegister();
        var instruction = floatToInt.TargetType.Signed ? "fptosi" : "fptoui";
        
        if (!operandValue.Contains('.') && !operandValue.Contains('e') && !operandValue.Contains('E'))
        {
            operandValue += ".0";
        }
        
        _context.EmitLine($"    {resultRegister} = {instruction} {operandType} {operandValue} to {targetLlvmType}");
    
        return (resultRegister, targetLlvmType);
    }
    
    private (string ReturnValue, LlvmType ReturnType) GenerateCastIntToFloat(CastIntToFloatNode intToFloat)
    {
        var (operandValue, operandType) = GenerateExpression(intToFloat.Operand);
        var targetLlvmType = new LlvmType.Float(intToFloat.TargetType.BitWidth);

        var resultRegister = _context.AllocateRegister();
        var instruction = intToFloat.SourceTypeIsSigned ? "sitofp" : "uitofp";

        _context.EmitLine($"    {resultRegister} = {instruction} {operandType} {operandValue} to {targetLlvmType}");

        return (resultRegister, targetLlvmType);
    }

    private (string ReturnValue, LlvmType ReturnType) GenerateExtendInteger(ExtendIntegerNode extendInt)
    {
        var (operandValue, operandType) = GenerateExpression(extendInt.Operand);
        var targetLlvmType = new LlvmType.Int(extendInt.TargetType.BitWidth);

        var resultRegister = _context.AllocateRegister();
        var instruction = extendInt.TargetType.Signed ? "sext" : "zext";

        _context.EmitLine($"    {resultRegister} = {instruction} {operandType} {operandValue} to {targetLlvmType}");

        return (resultRegister, targetLlvmType);
    }

    private (string ReturnValue, LlvmType ReturnType) GenerateExtendFloat(ExtendFloatNode extendFloat)
    {
        var (operandValue, operandType) = GenerateExpression(extendFloat.Operand);
        var targetLlvmType = new LlvmType.Float(extendFloat.TargetType.BitWidth);

        var resultRegister = _context.AllocateRegister();

        _context.EmitLine($"    {resultRegister} = fpext {operandType} {operandValue} to {targetLlvmType}");

        return (resultRegister, targetLlvmType);
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
        var variableCallee = node.Callee as VariableExpressionNode;
        var memberAccessCall = node.Callee as MemberAccessExpressionNode;

        if (variableCallee is null && memberAccessCall is null)
        {
            throw new ByronNotImplementedException(node.Callee.GetType(), this, node.SourceNode.Span);       
        }
        
        var evaluatedArguments = node.Arguments.Select(GenerateExpression).ToList();
        var argumentIr = string.Join(", ", evaluatedArguments.Select(arg => $"{arg.ReturnType} {arg.ReturnValue}"));

        var functionName = variableCallee?.Name ?? memberAccessCall?.MemberName ?? throw new UnreachableException();
        
        var llvmType = _context.GetFunctionReturnType(functionName);

        if (llvmType is LlvmType.Void)
        {
            _context.EmitLine($"    call void @{functionName}({argumentIr})");
            return ("void", new LlvmType.Void());
        }
        else
        {
            var resultRegister = _context.AllocateRegister();
            _context.EmitLine($"    {resultRegister} = call {llvmType} @{functionName}({argumentIr})");
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
            throw new ByronCodeGenerationException($"Type mismatch in binary expression: {leftLlvmType} and {rightLlvmType}");
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
            case BinaryOperator.BitwiseAnd:
            case BinaryOperator.LogicalAnd:
            case BinaryOperator.BitwiseOr:
            case BinaryOperator.LogicalOr:
            case BinaryOperator.BitwiseXor:
                var  instruction = LogicalOperationInstruction(node.Operator, isUnsigned);
                _context.EmitLine($"    {resultRegister} = {instruction} {leftLlvmType} {leftValue}, {rightValue}");
                break;
            case BinaryOperator.ShiftLeft:
                _context.EmitLine($"    {resultRegister} = shl {leftLlvmType} {leftValue}, {rightValue}");
                break;
            case BinaryOperator.ShiftRight:
                var shiftInstruction = isUnsigned ? "lshr" : "ashr";
                _context.EmitLine($"    {resultRegister} = {shiftInstruction} {leftLlvmType} {leftValue}, {rightValue}");
                break;
            default:
                throw new ByronNotImplementedException($"LLVM IR mapping for operator {node.Operator}", this, node.SourceNode.Span);
        }

        return (resultRegister, returnType);
    }

    private string LogicalOperationInstruction(BinaryOperator nodeOperator, bool isUnsigned)
    {
        return nodeOperator switch
        {
            BinaryOperator.BitwiseAnd or BinaryOperator.LogicalAnd => "and",
            BinaryOperator.BitwiseOr or BinaryOperator.LogicalOr => "or",
            BinaryOperator.BitwiseXor => "xor",

            _ => throw new InvalidOperationException($"Operation {nodeOperator} is not a logical operation")
        };
    }
    
    private string ArithmeticOperationInstruction(BinaryOperator nodeOperator, bool isFloat, bool isUnsigned)
    {
        return nodeOperator switch
        {
            BinaryOperator.Add => isFloat ? "fadd" : "add",
            BinaryOperator.Subtract => isFloat ? "fsub" : "sub",
            BinaryOperator.Multiply => isFloat ? "fmul" : "mul",
            BinaryOperator.Divide => isFloat ? "fdiv" : isUnsigned ? "udiv" : "sdiv",
            _ => throw new InvalidOperationException($"Operation {nodeOperator} is not an arithmetic operation")
        };
    }
    
    private string BooleanOperationInstruction(BinaryOperator nodeOperator, bool isFloat, bool isUnsigned)
    {
        return nodeOperator switch
        {
            BinaryOperator.Equal => isFloat ? "oeq" : "eq",
            BinaryOperator.NotEqual => isFloat ? "one" : "ne",
            
            BinaryOperator.LessThan => isFloat ? "olt" : isUnsigned ? "ult" : "slt",
            BinaryOperator.LessThanOrEqual => isFloat ? "ole" : isUnsigned ? "ule" : "sle",
            
            BinaryOperator.GreaterThan => isFloat ? "ogt" : isUnsigned ? "ugt" : "sgt",
            BinaryOperator.GreaterThanOrEqual => isFloat ? "oge" : isUnsigned ? "uge" : "sge",
            
            _ => throw new InvalidOperationException($"Operation {nodeOperator} is not a boolean operation")
        };
    }
}