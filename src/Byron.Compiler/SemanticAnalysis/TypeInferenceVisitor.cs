using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.SemanticAnalysis;

public class TypeInferenceVisitor
{
    private readonly TypeRegistry _typeRegistry;
    private readonly FunctionRegistry _functionRegistry;
    private readonly TypeMap _typeMap;
    private readonly SymbolTable _symbolTable;
    private readonly Diagnostics _diagnostics;

    public TypeInferenceVisitor(
        TypeRegistry typeRegistry,
        FunctionRegistry functionRegistry,
        TypeMap typeMap,
        SymbolTable symbolTable,
        Diagnostics diagnostics)
    {
        _typeRegistry = typeRegistry;
        _functionRegistry = functionRegistry;
        _typeMap = typeMap;
        _symbolTable = symbolTable;
        _diagnostics = diagnostics;
    }

    public void VisitFunction(FunctionDeclarationNode function)
    {
        _symbolTable.EnterScope();

        for (var i = 0; i < function.Parameters.Count; i++)
        {
            var parameter = function.Parameters[i];
            if (parameter.Name == "self")
            {
                if (i != 0)
                {
                    _diagnostics.InvalidSelfArgument(function, parameter);
                }

                string[] expectedModulePath = [..parameter.Type.CanonicalName.ModulePath, parameter.Type.CanonicalName.ShortName];
                var isValidSelfType =
                    function.CanonicalName.ModulePath.SequenceEqual(expectedModulePath) 
                    && string.Equals(parameter.Type.CanonicalName.ShortName, function.CanonicalName.ModulePath.LastOrDefault());

                if (!isValidSelfType)
                {
                    _diagnostics.InvalidSelfArgument(
                        parameter.Type.CanonicalName.ToString(),
                        function.CanonicalName.ToModulePathString(),
                        function);
                }
            }

            var isMutable = parameter.Ownership.IsMutable();

            var type = parameter.Ownership.IsReference()
                ? new ReferenceTypeNode(parameter.Type, isMutable, parameter.Span)
                : parameter.Type;

            _symbolTable.Declare(parameter.Name, type, isMutable);
        }

        VisitBlock(function.Body);

        _symbolTable.ExitScope();
    }

    public void VisitBlock(BlockStatementNode block)
    {
        foreach (var statement in block.Statements)
        {
            VisitStatement(statement);
        }
    }

    private void VisitStatement(StatementNode statement)
    {
        switch (statement)
        {
            case VariableDeclarationNode variable:
                VisitVariableDeclaration(variable);
                break;
            case ExpressionStatementNode expressionStatement:
                _ = VisitExpression(expressionStatement.Expression);
                break;
            case AssignmentStatementNode assignment:
                VisitAssignmentStatement(assignment);
                break;
            case IfElseStatement ifElse:
                VisitIfElseStatement(ifElse);
                break;
            case ReturnStatementNode @return:
                VisitReturnStatement(@return);
                break;
            case WhileStatement @while:
                VisitStatementWhile(@while);
                break;
        }
    }

    private void VisitStatementWhile(WhileStatement @while)
    {
        _ = VisitExpression(@while.ContinuationCondition);
        VisitBlock(@while.Body);
    }

    private void VisitReturnStatement(ReturnStatementNode @return)
    {
        if (@return.Expression is not null)
        {
            _ = VisitExpression(@return.Expression);
            // todo: Type check expression vs current function return type using TryCoerce.
        }
    }

    private void VisitIfElseStatement(IfElseStatement ifElse)
    {
        _ = VisitExpression(ifElse.Condition);
        var conditionType = _typeMap.GetType(ifElse.Condition);
        if (conditionType is not BoolTypeNode)
        {
            _diagnostics.TypeMismatch(conditionType, PrimitiveTypeNames.boolean);
            return;
        }

        VisitBlock(ifElse.ThenBranch);

        if (ifElse.ElseBranch != null)
        {
            VisitBlock(ifElse.ElseBranch);
        }
    }

    private void VisitAssignmentStatement(AssignmentStatementNode assignment)
    {
        assignment.Target = VisitExpression(assignment.Target);
        assignment.Value = VisitExpression(assignment.Value);

        var targetType = _typeMap.GetType(assignment.Target);

        if (!TryCoerce(assignment.Value, targetType, out var coercedValue))
        {
            var valueType = _typeMap.GetType(assignment.Value);
            _diagnostics.TypeMismatch(targetType, valueType);
            return;
        }

        assignment.Value = coercedValue;

        if (assignment.Target is VariableExpressionNode variable)
        {
            if (_symbolTable.TryGet(variable.Name, out var symbol) && !symbol.IsMutable)
            {
                _diagnostics.InvalidMutation(variable, symbol.Type.Span);
            }
        }
    }

    private ExpressionNode VisitExpression(ExpressionNode expression)
    {
        return expression switch
        {
            UnaryExpressionNode unary => VisitUnaryExpression(unary),
            IntegerLiteralNode integerLiteral => VisitIntegerLiteralNode(integerLiteral),
            FloatLiteralNode floatLiteral => VisitFloatLiteralNode(floatLiteral),
            BooleanLiteralNode booleanLiteral => VisitBooleanLiteralNode(booleanLiteral),
            StructFieldInitializationExpressionNode structFieldInitializationExpression => VisitStructFieldInitializationExpression(structFieldInitializationExpression),
            VariableExpressionNode variableExpression => VisitVariableExpression(variableExpression),
            CallExpressionNode callExpression => VisitCallExpressionNode(callExpression),
            MemberAccessExpressionNode memberAccess => VisitMemberExpressionNode(memberAccess),
            BinaryExpressionNode binaryExpressionNode => VisitBinaryExpressionNode(binaryExpressionNode),
            AddressOfExpressionNode addressOf => VisitAddressOfExpression(addressOf),
            DereferenceExpressionNode dereference => VisitDereferenceExpressionNode(dereference),

            _ => expression
        };
    }

    private ExpressionNode VisitBooleanLiteralNode(BooleanLiteralNode booleanLiteral)
    {
        _typeMap.SetType(booleanLiteral, new BoolTypeNode(booleanLiteral.Span));
        return booleanLiteral;
    }

    private ExpressionNode VisitFloatLiteralNode(FloatLiteralNode floatLiteral)
    {
        var float32Type = new Float32TypeNode(floatLiteral.Span);
        FloatTypeNode floatType = TypeBounds.CanCoerceToType(floatLiteral.Value, float32Type)
            ? float32Type
            : new Float64TypeNode(floatLiteral.Span);
        _typeMap.SetType(floatLiteral, floatType);
        return floatLiteral;
    }

    private ExpressionNode VisitIntegerLiteralNode(IntegerLiteralNode integerLiteral)
    {
        var int32Type = new Int32TypeNode(integerLiteral.Span);
        SignedIntTypeNode intType = TypeBounds.CanCoerceToType(integerLiteral.Value, int32Type)
            ? int32Type
            : new Int64TypeNode(integerLiteral.Span);
        _typeMap.SetType(integerLiteral, intType);
        return integerLiteral;
    }

    private ExpressionNode VisitDereferenceExpressionNode(DereferenceExpressionNode dereference)
    {
        dereference.Target = VisitExpression(dereference.Target);
        var targetType = _typeMap.GetType(dereference.Target);

        if (targetType is ReferenceTypeNode referenceTypeNode)
        {
            _typeMap.SetType(dereference, referenceTypeNode.Target);
        }
        else
        {
            _diagnostics.InvalidDereference(dereference, targetType);
        }

        return dereference;
    }

    private ExpressionNode VisitAddressOfExpression(AddressOfExpressionNode addressOf)
    {
        addressOf.Target = VisitExpression(addressOf.Target);
        var targetType = _typeMap.GetType(addressOf.Target);

        var referenceType = new ReferenceTypeNode(targetType, addressOf.IsMutable, addressOf.Span);
        _typeMap.SetType(addressOf, referenceType);
        return addressOf;
    }

    private ExpressionNode VisitUnaryExpression(UnaryExpressionNode unary)
    {
        unary.Operand = VisitExpression(unary.Operand);
        var operandType = _typeMap.GetType(unary.Operand);

        switch (unary.Operator)
        {
            case UnaryOperator.Negative:
            {
                if (operandType is not SignedIntTypeNode and not FloatTypeNode)
                {
                    _diagnostics.InvalidUnaryOperation(unary, operandType);
                }

                _typeMap.SetType(unary, operandType);
                return unary;
            }
            case UnaryOperator.Not:
            {
                if (operandType is not BoolTypeNode)
                {
                    _diagnostics.InvalidUnaryOperation(unary, operandType);
                }

                _typeMap.SetType(unary, new BoolTypeNode(unary.Span));
                return unary;
            }
            default:
                throw new ByronNotImplementedException(unary.Operator.ToString(), this, unary.Span);
        }
    }

    private ExpressionNode VisitStructFieldInitializationExpression(
        StructFieldInitializationExpressionNode initialization)
    {
        _typeMap.SetType(initialization, initialization.NominalType);
        var structName = initialization.NominalType.CanonicalName;
        if (!_typeRegistry.TryGetStruct(structName, out var structDeclaration))
        {
            _diagnostics.UndeclaredType(initialization.NominalType);
            return initialization;
        }

        foreach (var fieldInitializer in initialization.FieldInitializers)
        {
            fieldInitializer.Value = VisitExpression(fieldInitializer.Value);

            var matchingField = structDeclaration.Fields.FirstOrDefault(f => f.Name == fieldInitializer.FieldName);
            if (matchingField is null)
            {
                _diagnostics.MissingMember(structName, fieldInitializer);
                continue;
            }

            if (!TryCoerce(fieldInitializer.Value, matchingField.Type, out var coercedValue))
            {
                var valueType = _typeMap.GetType(fieldInitializer.Value);
                _diagnostics.TypeMismatch(matchingField.Type, valueType);
            }
            else
            {
                fieldInitializer.Value = coercedValue;
            }
        }

        return initialization;
    }

    private ExpressionNode VisitBinaryExpressionNode(BinaryExpressionNode binaryExpression)
    {
        binaryExpression.Left = VisitExpression(binaryExpression.Left);
        binaryExpression.Right = VisitExpression(binaryExpression.Right);

        var leftType = _typeMap.GetType(binaryExpression.Left);
        var rightType = _typeMap.GetType(binaryExpression.Right);

        if (binaryExpression.Operator.IsRelationalComparison())
        {
            var boolType = new BoolTypeNode(binaryExpression.Span);
            _typeMap.SetType(binaryExpression, boolType);

            if (TryGetPreferredCoercionType(binaryExpression.Left, leftType, binaryExpression.Right, rightType,
                    out var coercedType))
            {
                binaryExpression.Left = AddCastsWhenRequired(binaryExpression.Left, coercedType);
                binaryExpression.Right = AddCastsWhenRequired(binaryExpression.Right, coercedType);
            }
            else
            {
                _diagnostics.TypeMismatch(leftType, rightType);
            }

            return binaryExpression;
        }

        if (TryGetPreferredCoercionType(binaryExpression.Left, leftType, binaryExpression.Right, rightType,
                out var coerced))
        {
            binaryExpression.Left = AddCastsWhenRequired(binaryExpression.Left, coerced);
            binaryExpression.Right = AddCastsWhenRequired(binaryExpression.Right, coerced);
            _typeMap.SetType(binaryExpression, coerced);
        }
        else
        {
            _diagnostics.TypeMismatch(leftType, rightType);
        }

        return binaryExpression;
    }

    private ExpressionNode AddCastsWhenRequired(ExpressionNode expression, TypeNode targetType)
    {
        var sourceType = _typeMap.GetType(expression);

        if (sourceType.CanonicalName == targetType.CanonicalName)
        {
            return expression;
        }

        var cast = Cast(expression, sourceType, targetType);
        _typeMap.SetType(cast, targetType);
        return cast;
    }

    private ExpressionNode Cast(ExpressionNode operand, TypeNode sourceType, TypeNode targetType)
    {
        if (sourceType is IntegerTypeNode sourceInt && targetType is FloatTypeNode targetFloat)
        {
            return new CastIntToFloatNode(operand, targetFloat, sourceInt.Signed, operand.Span);
        }

        if (sourceType is FloatTypeNode && targetType is IntegerTypeNode targetInt)
        {
            if (operand is FloatLiteralNode floatLit && !TypeBounds.CanCoerceToType(floatLit.Value, targetInt))
            {
                return operand;
            }

            return new CastFloatToIntNode(operand, targetInt, targetInt.Signed, operand.Span);
        }

        if (sourceType is IntegerTypeNode sourceIntToWiden && targetType is IntegerTypeNode widerInt)
        {
            if (sourceIntToWiden.BitWidth < widerInt.BitWidth)
            {
                return new ExtendIntegerNode(operand, widerInt, operand.Span);
            }

            return operand;
        }

        if (sourceType is FloatTypeNode sourceFloatToWiden && targetType is FloatTypeNode widerFloat &&
            sourceFloatToWiden.BitWidth < widerFloat.BitWidth)
        {
            return new ExtendFloatNode(operand, widerFloat, operand.Span);
        }

        return operand;
    }

    private bool TryGetPreferredCoercionType(ExpressionNode leftExpression, TypeNode leftType,
        ExpressionNode rightExpression, TypeNode rightType, [NotNullWhen(true)] out TypeNode? preferredType)
    {
        preferredType = null;

        if (leftType.CanonicalName == rightType.CanonicalName)
        {
            preferredType = leftType;
            return true;
        }

        if (leftType is IntegerTypeNode integerLeft && rightType is FloatTypeNode &&
            TypeBounds.CanCoerceToType(rightExpression, integerLeft))
        {
            preferredType = leftType;
            return true;
        }

        if (rightType is IntegerTypeNode integerRight && leftType is FloatTypeNode &&
            TypeBounds.CanCoerceToType(leftExpression, integerRight))
        {
            preferredType = rightType;
            return true;
        }

        return false;
    }

    private void VisitVariableDeclaration(VariableDeclarationNode variableDeclaration)
    {
        if (_symbolTable.TryGet(variableDeclaration.Name, out _))
        {
            _diagnostics.Duplicate(variableDeclaration);
            _ = VisitExpression(variableDeclaration.Initializer);
            return;
        }

        variableDeclaration.Initializer = VisitExpression(variableDeclaration.Initializer);
        var inferredType = _typeMap.GetType(variableDeclaration.Initializer);
        var finalType = inferredType;

        if (variableDeclaration.TypeAnnotation is not null)
        {
            if (!_typeRegistry.IsValidType(variableDeclaration.TypeAnnotation))
            {
                _diagnostics.UndeclaredType(variableDeclaration.TypeAnnotation);
                return;
            }

            if (!TryCoerce(variableDeclaration.Initializer, variableDeclaration.TypeAnnotation, out var coercedInitializer))
            {
                _diagnostics.TypeMismatch(variableDeclaration.TypeAnnotation, inferredType);
                return;
            }

            variableDeclaration.Initializer = coercedInitializer;
            finalType = variableDeclaration.TypeAnnotation;
        }

        _symbolTable.Declare(variableDeclaration.Name, finalType, variableDeclaration.IsMutable);
    }

    private ExpressionNode VisitVariableExpression(VariableExpressionNode variableExpression)
    {
        if (_symbolTable.TryGet(variableExpression.Name, out var symbol))
        {
            _typeMap.SetType(variableExpression, symbol.Type);
        }
        else
        {
            _diagnostics.UndeclaredVariable(variableExpression);
        }

        return variableExpression;
    }

    private ExpressionNode VisitCallExpressionNode(CallExpressionNode callExpression)
    {
        CallExpressionNode functionInvocation = callExpression;

        for (var i = 0; i < callExpression.Arguments.Count; i++)
        {
            callExpression.Arguments[i] = VisitExpression(callExpression.Arguments[i]);
        }

        string[] modulePath;
        string functionName;

        if (callExpression.Callee is VariableExpressionNode variableExpression)
        {
            functionName = variableExpression.Name;
            modulePath = [];
        }
        else if (callExpression.Callee is MemberAccessExpressionNode memberAccess)
        {
            if (memberAccess.Target is VariableExpressionNode targetVariableExpression)
            {
                if (_typeRegistry.TryGetStruct(targetVariableExpression.Name, out var targetStruct))
                {
                    modulePath = [.. targetStruct.ModulePath, targetStruct.Name];
                }
                else if (_symbolTable.TryGet(targetVariableExpression.Name, out var symbol))
                {
                    memberAccess.Target = VisitExpression(targetVariableExpression);
                    functionInvocation = new MethodCallExpression(memberAccess.Target, memberAccess, callExpression.Arguments, callExpression.Span);
                    var targetType = _typeMap.GetType(memberAccess.Target); 
                    modulePath = [.. targetType.CanonicalName.ModulePath, targetType.CanonicalName.ShortName];
                }
                else
                {
                    modulePath = [targetVariableExpression.Name];
                }
            } 
            else
            {
                memberAccess.Target = VisitExpression(memberAccess.Target);

                if (_typeMap.TryGetType(memberAccess.Target, out var targetType))
                {
                    modulePath = [.. targetType.CanonicalName.ModulePath, targetType.CanonicalName.ShortName];
                    functionInvocation = new MethodCallExpression(memberAccess.Target, memberAccess, callExpression.Arguments, callExpression.Span);
                }
                else
                {
                    modulePath = [];
                }
            }

            functionName = memberAccess.MemberName;
        }
        else
        {
            throw new ByronNotImplementedException(callExpression.Callee.GetType(), this, callExpression.Span);
        }

        if (!_functionRegistry.TryGetFunctionInScope(modulePath, functionName, out var function))
        {
            _diagnostics.UndeclaredFunction(functionName, callExpression.Callee.Span);
            return functionInvocation;
        }

        return functionInvocation switch
        {
            MethodCallExpression methodCall => TryCoerceAllArguments(methodCall, function),
            _ => TryCoerceAllArguments(functionInvocation, function)
        };
    }

    private CallExpressionNode TryCoerceAllArguments(MethodCallExpression methodCall, FunctionSymbol function)
    {
        if (methodCall.Arguments.Count + 1 != function.Parameters.Count)
        {
            _diagnostics.InvalidArgumentCount(methodCall, function);
        }

        ExpressionNode[] arguments = [methodCall.Receiver, ..methodCall.Arguments];
        var maximumArgumentCount = Math.Min(arguments.Length, function.Parameters.Count);

        for (var i = 0; i < maximumArgumentCount; i++)
        {
            var argument = arguments[i];
            var argumentType = _typeMap.GetType(argument);
            var parameterType = function.Parameters[i].Type;

            if (!TryCoerce(argument, parameterType, out var coercedExpression))
            {
                _diagnostics.InvalidArgument(argumentType.CanonicalName, parameterType.CanonicalName,
                    function.CanonicalName, methodCall.Span);
                return methodCall;
            }

            if (i == 0)
            {
                methodCall.Receiver = coercedExpression;
            }
            else
            {
                methodCall.Arguments[i - 1] = coercedExpression;
            }
        }

        _typeMap.SetType(methodCall, function.ReturnType);
        return methodCall;
    }

    private CallExpressionNode TryCoerceAllArguments(CallExpressionNode callExpression, FunctionSymbol function)
    {
        if (callExpression.Arguments.Count != function.Parameters.Count)
        {
            _diagnostics.InvalidArgumentCount(callExpression, function);
        }

        var maximumArgumentCount = Math.Min(callExpression.Arguments.Count, function.Parameters.Count);

        for (var i = 0; i < maximumArgumentCount; i++)
        {
            var argument = callExpression.Arguments[i];
            var argumentType = _typeMap.GetType(argument);
            var parameterType = function.Parameters[i].Type;

            if (!TryCoerce(argument, parameterType, out var coercedExpression))
            {
                _diagnostics.InvalidArgument(argumentType.CanonicalName, parameterType.CanonicalName,
                    function.CanonicalName, callExpression.Span);
                return callExpression;
            }

            callExpression.Arguments[i] = coercedExpression;
        }

        _typeMap.SetType(callExpression, function.ReturnType);
        return callExpression;
    }

    private ExpressionNode VisitMemberExpressionNode(MemberAccessExpressionNode memberAccess)
    {
        memberAccess.Target = VisitExpression(memberAccess.Target);
        var targetType = _typeMap.GetType(memberAccess.Target);

        var targetTypeCanonicalName = targetType.CanonicalName;
        if (_typeRegistry.TryGetStruct(targetTypeCanonicalName, out var structDeclaration))
        {
            var field = structDeclaration.Fields.FirstOrDefault(f => f.Name == memberAccess.MemberName);
            if (field is not null)
            {
                _typeMap.SetType(memberAccess, field.Type);
            }
            else
            {
                _diagnostics.MissingMember(targetTypeCanonicalName, memberAccess);
            }
        }
        else
        {
            _diagnostics.MissingMember(targetTypeCanonicalName, memberAccess);
        }

        return memberAccess;
    }

    private bool TryCoerce(ExpressionNode expression, TypeNode targetType,
        [NotNullWhen(true)] out ExpressionNode? result)
    {
        var sourceType = _typeMap.GetType(expression);

        if (sourceType.CanonicalName == targetType.CanonicalName)
        {
            result = expression;
            return true;
        }

        if (targetType is ReferenceTypeNode targetRef && sourceType.CanonicalName == targetRef.Target.CanonicalName)
        {
            result = new AddressOfExpressionNode(expression, targetRef.IsMutable, expression.Span);
            _typeMap.SetType(result, targetType);
            return true;
        }

        if (sourceType is ReferenceTypeNode sourceRef && sourceRef.Target.CanonicalName == targetType.CanonicalName)
        {
            result = new DereferenceExpressionNode(expression, expression.Span);
            _typeMap.SetType(result, targetType);
            return true;
        }

        if (expression is IntegerLiteralNode intLiteral && targetType is NumericTypeNode targetNumeric)
        {
            if (TypeBounds.CanCoerceToType(intLiteral.Value, targetNumeric))
            {
                result = expression;
                _typeMap.SetType(result, targetType);
                return true;
            }

            result = null;
            return false;
        }

        if (expression is FloatLiteralNode floatLiteral && targetType is NumericTypeNode targetFloatNumeric)
        {
            if (TypeBounds.CanCoerceToType(floatLiteral.Value, targetFloatNumeric))
            {
                result = expression;
                _typeMap.SetType(result, targetType);
                return true;
            }

            result = null;
            return false;
        }

        if (sourceType is IntegerTypeNode sourceInt && targetType is FloatTypeNode targetFloat)
        {
            result = new CastIntToFloatNode(expression, targetFloat, sourceInt.Signed, expression.Span);
            _typeMap.SetType(result, targetType);
            return true;
        }

        if (sourceType is FloatTypeNode && targetType is IntegerTypeNode targetInt)
        {
            if (expression is FloatLiteralNode floatLit && !TypeBounds.CanCoerceToType(floatLit.Value, targetInt))
            {
                result = null;
                return false;
            }

            result = new CastFloatToIntNode(expression, targetInt, targetInt.Signed, expression.Span);
            _typeMap.SetType(result, targetType);
            return true;
        }

        if (sourceType is IntegerTypeNode sourceIntToWiden && targetType is IntegerTypeNode widerInt &&
            sourceIntToWiden.BitWidth < widerInt.BitWidth)
        {
            result = new ExtendIntegerNode(expression, widerInt, expression.Span);
            _typeMap.SetType(result, targetType);
            return true;
        }

        if (sourceType is FloatTypeNode sourceFloatToWiden && targetType is FloatTypeNode widerFloat &&
            sourceFloatToWiden.BitWidth < widerFloat.BitWidth)
        {
            result = new ExtendFloatNode(expression, widerFloat, expression.Span);
            _typeMap.SetType(result, targetType);
            return true;
        }

        result = null;
        return false;
    }
}