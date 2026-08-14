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
        VisitExpression(@while.ContinuationCondition);
        VisitBlock(@while.Body);
    }

    private void VisitReturnStatement(ReturnStatementNode @return)
    {
        if (@return.Expression is not null)
        {
            VisitExpression(@return.Expression);
            // todo: Type check expression vs current function return type.
        }
    }

    private void VisitIfElseStatement(IfElseStatement ifElse)
    {
        VisitExpression(ifElse.Condition);
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
        VisitExpression(assignment.Target);
        VisitExpression(assignment.Value);

        var targetType = _typeMap.GetType(assignment.Target);
        var valueType = _typeMap.GetType(assignment.Value);

        if (!IsAssignable(assignment, valueType))
        {
            _diagnostics.TypeMismatch(targetType, valueType);
            return;
        }

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
            var valueType = _typeMap.GetType(fieldInitializer.Value);

            var matchingField = structDeclaration.Fields.FirstOrDefault(f => f.Name == fieldInitializer.FieldName);
            if (matchingField is null)
            {
                _diagnostics.MissingMember(structName, fieldInitializer);
                continue;
            }

            if (!IsAssignable(fieldInitializer.Value, valueType, matchingField.Type))
            {
                _diagnostics.TypeMismatch(matchingField.Type, valueType);
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

        //todo: Update to "just" set the casts from the existing try get preferred coersion type. Probably just "Coerce" who sets diagnostics typemismatch
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
            VisitExpression(variableDeclaration.Initializer);
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

            if (!IsAssignable(variableDeclaration, inferredType))
            {
                _diagnostics.TypeMismatch(variableDeclaration.TypeAnnotation, inferredType);
                return;
            }

            finalType = variableDeclaration.TypeAnnotation;
        }

        _symbolTable.Declare(variableDeclaration.Name, finalType, variableDeclaration.IsMutable);
    }

    private bool IsAssignable(VariableDeclarationNode variableDeclaration, TypeNode assignedValueType)
    {
        if (variableDeclaration.TypeAnnotation is null)
        {
            return true;
        }

        var declaredType = variableDeclaration.TypeAnnotation;
        var declaredTypeName = declaredType.CanonicalName;
        var assignedValueTypeName = assignedValueType.CanonicalName;

        if (string.Equals(declaredTypeName, assignedValueTypeName))
        {
            return true;
        }

        if (declaredType is NumericTypeNode declaredNumeric && assignedValueType is NumericTypeNode)
        {
            if (variableDeclaration.Initializer is IntegerLiteralNode intLiteral)
            {
                if (TypeBounds.CanCoerceToType(intLiteral.Value, declaredNumeric))
                {
                    return true;
                }

                _diagnostics.OutOfRange(intLiteral, declaredNumeric);
            }
        }

        return false;
    }

    private bool IsAssignable(ExpressionNode expression, TypeNode assignedType, TypeNode targetType)
    {
        if (assignedType.CanonicalName == targetType.CanonicalName)
        {
            return true;
        }

        if (assignedType is not NumericTypeNode assignedNumeric || targetType is not NumericTypeNode targetNumeric)
        {
            return false;
        }

        if (expression is IntegerLiteralNode intLiteral && TypeBounds.CanCoerceToType(intLiteral.Value, targetNumeric))
        {
            return true;
        }

        if (expression is FloatLiteralNode floatLiteral &&
            TypeBounds.CanCoerceToType(floatLiteral.Value, targetNumeric))
        {
            return true;
        }

        return (assignedNumeric, targetNumeric) switch
        {
            (SignedIntTypeNode assigned, SignedIntTypeNode target) => assigned.BitWidth < target.BitWidth,
            (UnsignedIntTypeNode assigned, UnsignedIntTypeNode target) => assigned.BitWidth < target.BitWidth,
            (UnsignedIntTypeNode assigned, SignedIntTypeNode target) => assigned.BitWidth < target.BitWidth,
            (SignedIntTypeNode, UnsignedIntTypeNode) => false,
            (SignedIntTypeNode assigned, FloatTypeNode target) => target.BitWidth switch
            {
                32 => assigned.BitWidth <= 16,
                64 => assigned.BitWidth <= 32,
                _ => false
            },
            (UnsignedIntTypeNode assigned, FloatTypeNode target) => target.BitWidth switch
            {
                32 => assigned.BitWidth <= 16,
                64 => assigned.BitWidth <= 32,
                _ => false
            },
            (FloatTypeNode assigned, FloatTypeNode target) => assigned.BitWidth < target.BitWidth,
            _ => false
        };
    }

    private bool IsAssignable(ExpressionNode expression, TypeNode assignedType, TypeNode targetType,
        [NotNullWhen(true)] out ExpressionNode? coercedExpression)
    {

        coercedExpression = expression;

        if (assignedType.CanonicalName == targetType.CanonicalName)
        {
            return true;
        }

        coercedExpression = AddCoercionsWhenRequired(expression, targetType);
        return _typeMap.GetType(coercedExpression).CanonicalName == targetType.CanonicalName;
    }

    private ExpressionNode AddCoercionsWhenRequired(ExpressionNode expression, TypeNode targetType)
    {
        var sourceType = _typeMap.GetType(expression);

        if (sourceType.CanonicalName == targetType.CanonicalName)
        {
            return expression;
        }


        //todo: just return the coercion, no need for the try stuff. 
        if (TryCreateCoercionNode(expression, sourceType, targetType, out var coercionNode))
        {
            _typeMap.SetType(coercionNode, targetType);
            return coercionNode;
        }

        return expression;
    }

    private bool TryCreateCoercionNode(
        ExpressionNode operand,
        TypeNode sourceType,
        TypeNode targetType,
        [NotNullWhen(true)] out ExpressionNode? coercionNode)
    {
        coercionNode = null;

        if (targetType is ReferenceTypeNode targetReferenceType &&
            sourceType.CanonicalName == targetReferenceType.Target.CanonicalName)
        {
            coercionNode = new AddressOfExpressionNode(operand, targetReferenceType.IsMutable, operand.Span);
            return true;
        }

        if (sourceType is ReferenceTypeNode sourceReferenceType &&
            sourceReferenceType.Target.CanonicalName == targetType.CanonicalName)
        {
            coercionNode = new DereferenceExpressionNode(operand, operand.Span);
            return true;
        }

        if (sourceType is IntegerTypeNode sourceInt && targetType is FloatTypeNode targetFloat)
        {
            coercionNode = new CastIntToFloatNode(operand, targetFloat, sourceInt.Signed, operand.Span);
            return true;
        }

        if (sourceType is FloatTypeNode && targetType is IntegerTypeNode targetInt)
        {
            if (operand is FloatLiteralNode floatLit && !TypeBounds.CanCoerceToType(floatLit.Value, targetInt))
            {
                return false;
            }

            coercionNode = new CastFloatToIntNode(operand, targetInt, targetInt.Signed, operand.Span);
            return true;
        }

        if (sourceType is IntegerTypeNode sourceIntToWiden && targetType is IntegerTypeNode widerInt)
        {
            if (sourceIntToWiden.BitWidth < widerInt.BitWidth)
            {
                coercionNode = new ExtendIntegerNode(operand, widerInt, operand.Span);
                return true;
            }

            return false;
        }

        if (sourceType is FloatTypeNode sourceFloatToWiden && targetType is FloatTypeNode widerFloat &&
            sourceFloatToWiden.BitWidth < widerFloat.BitWidth)
        {
            coercionNode = new ExtendFloatNode(operand, widerFloat, operand.Span);
            return true;
        }

        return false;
    }

    private bool IsAssignable(AssignmentStatementNode assignment, TypeNode assignedValueType)
    {
        var targetType = _typeMap.GetType(assignment.Target);
        var declaredTypeName = targetType.CanonicalName;
        var assignedValueTypeName = assignedValueType.CanonicalName;

        if (string.Equals(declaredTypeName, assignedValueTypeName))
        {
            return true;
        }

        if (targetType is NumericTypeNode declaredNumeric && assignedValueType is NumericTypeNode)
        {
            if (assignment.Value is IntegerLiteralNode intLiteral)
            {
                if (TypeBounds.CanCoerceToType(intLiteral.Value, declaredNumeric))
                {
                    return true;
                }

                _diagnostics.OutOfRange(intLiteral, declaredNumeric);
            }
        }

        return false;
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
        TypeNode targetType;

        if (callExpression.Callee is VariableExpressionNode variableExpression)
        {
            functionName = variableExpression.Name;
            modulePath = [];
        }
        else if (callExpression.Callee is MemberAccessExpressionNode memberAccess)
        {
            if (memberAccess.Target is VariableExpressionNode targetVariableExpression)
            {
                if(_typeRegistry.TryGetStruct(targetVariableExpression.Name, out var targetStruct))
                {
                    modulePath = [.. targetStruct.ModulePath, targetStruct.Name];
                }
                else if (_symbolTable.TryGet(targetVariableExpression.Name, out var symbol))
                {
                    memberAccess.Target = VisitExpression(targetVariableExpression);
                    functionInvocation = new MethodCallExpression(memberAccess.Target, callExpression.Arguments, callExpression.Span);
                    targetType = _typeMap.GetType(memberAccess.Target); 
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
        
                if (_typeMap.TryGetType(memberAccess.Target, out targetType))
                {
                    modulePath = targetType.CanonicalName.ModulePath;
                    functionInvocation = new MethodCallExpression(memberAccess.Target, callExpression.Arguments, callExpression.Span);
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
        // todo: This should be hit when functionInvocation is set to MethodCall. Check that.
        if (methodCall.Arguments.Count + 1 !=
            function.Parameters.Count) // self should be the first argument for a method call
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







    /// <summary>
    /// Attempts to coerce an expression to a target type.
    /// If coercion is valid, returns true and populates result with the (possibly wrapped) expression,
    /// updating _typeMap accordingly.
    /// </summary>
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
            result = new CastFloatToIntNode(expression, targetInt, targetInt.Signed, expression.Span);
            _typeMap.SetType(result, targetType);
            return true;
        }

        if (sourceType is IntegerTypeNode sourceIntWiden && targetType is IntegerTypeNode widerInt)
        {
            if (sourceIntWiden.BitWidth < widerInt.BitWidth)
            {
                result = new ExtendIntegerNode(expression, widerInt, expression.Span);
                _typeMap.SetType(result, targetType);
                return true;
            }
        }

        if (sourceType is FloatTypeNode sourceFloatWiden && targetType is FloatTypeNode widerFloat)
        {
            if (sourceFloatWiden.BitWidth < widerFloat.BitWidth)
            {
                result = new ExtendFloatNode(expression, widerFloat, expression.Span);
                _typeMap.SetType(result, targetType);
                return true;
            }
        }

        result = null;
        return false;
    }
}





