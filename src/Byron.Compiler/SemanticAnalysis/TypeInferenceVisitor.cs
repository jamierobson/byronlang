using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.SemanticAnalysis;

public class TypeInferenceVisitor
{
    private readonly TypeMap _typeMap;
    private readonly ScopedSymbolTable _scopedSymbolTable;
    private readonly Diagnostics _diagnostics;
    private readonly GlobalSymbolTableLookup _globalSymbolTableLookup;
    
    public TypeInferenceVisitor(
        GlobalSymbolTableLookup globalSymbolTableLookup,
        TypeMap typeMap,
        ScopedSymbolTable scopedSymbolTable,
        Diagnostics diagnostics)
    {
        _globalSymbolTableLookup = globalSymbolTableLookup;
        _typeMap = typeMap;
        _scopedSymbolTable = scopedSymbolTable;
        _diagnostics = diagnostics;
    }

    private void TryCanonize(ParameterNode parameter, FunctionDeclarationContext context)
    {
        parameter.Type = CanonizedOrProvidedValue(parameter.Type, context);
    }

    private void TryCanonize(FunctionSignatureNode signature, FunctionDeclarationContext context)
    {
        signature.ReturnType = CanonizedOrProvidedValue(signature.ReturnType, context);
    }

    private TypeNode CanonizedOrProvidedValue(TypeNode type, FunctionDeclarationContext context)
    {
        if (_globalSymbolTableLookup.TryResolveCanonicalType(type, context.ImplementBlock?.Symbol.Segments ??  context.Module.Symbol.Segments, context.Module, out var lookup))
        {
            return lookup;
        }
        
        _diagnostics.UndeclaredType(type, "function argument");
        return type;
    }
    
    public void VisitFunction(FunctionDeclarationNode function, FunctionDeclarationContext declarationContext)
    {
        _scopedSymbolTable.EnterScope();

        TryCanonize(function.Signature, declarationContext);

        for (var i = 0; i < function.Signature.Parameters.Count; i++)
        {
            var parameter = function.Signature.Parameters[i];
            if (parameter.Name == ParameterNode.SelfArgumentName)
            {
                if (i != 0)
                {
                    _diagnostics.InvalidSelfArgumentPosition(function, parameter);
                }

                if (declarationContext.ImplementBlock is null)
                {
                    _diagnostics.InvalidSelfArgumentOutsideOfImplementBlock(function.Signature, parameter.Span);
                }
                else
                {
                    var expectedParameterType = declarationContext.ImplementBlock.TypeNode;
                    var actualParameterType = parameter.Type;

                    if (!_globalSymbolTableLookup.TryResolveCanonicalType(actualParameterType, declarationContext.ImplementBlock.Symbol.Segments, declarationContext.Module, out var canonicalType))
                    {
                        _diagnostics.UndeclaredType(actualParameterType);
                    }

                    if (expectedParameterType == canonicalType)
                    {
                        parameter.Type = canonicalType;
                    }
                    else
                    {
                        _diagnostics.InvalidSelfArgumentType(
                        parameter.Type.Symbol.ToString(),
                        function.Symbol.ToString(),
                        function);
                    }
                }
            }

            TryCanonize(parameter, declarationContext);

            var isMutable = parameter.Ownership.IsMutable();

            var type = parameter.Ownership.IsReference()
                ? new ReferenceTypeNode(parameter.Type, isMutable, parameter.Span)
                : parameter.Type;

            _scopedSymbolTable.Declare(parameter.Name, type, isMutable);
        }

        VisitBlock(declarationContext.Module, function.Body);

        _scopedSymbolTable.ExitScope();
    }

    public void VisitBlock(ModuleDeclarationNode module, BlockStatementNode block)
    {
        foreach (var statement in block.Statements)
        {
            VisitStatement(module, statement);
        }
    }

    private void VisitStatement(ModuleDeclarationNode module, StatementNode statement)
    {
        switch (statement)
        {
            case VariableDeclarationNode variable:
                VisitVariableDeclaration(module, variable);
                break;
            case ExpressionStatementNode expressionStatement:
                expressionStatement.Expression = VisitExpression(module, expressionStatement.Expression);
                break;
            case AssignmentStatementNode assignment:
                VisitAssignmentStatement(module, assignment);
                break;
            case IfElseStatement ifElse:
                VisitIfElseStatement(module, ifElse);
                break;
            case ReturnStatementNode @return:
                VisitReturnStatement(module, @return);
                break;
            case WhileStatement @while:
                VisitStatementWhile(module, @while);
                break;
        }
    }

    private void VisitStatementWhile(ModuleDeclarationNode module, WhileStatement @while)
    {
        _ = VisitExpression(module, @while.ContinuationCondition);
        VisitBlock(module, @while.Body);
    }

    private void VisitReturnStatement(ModuleDeclarationNode module, ReturnStatementNode @return)
    {
        if (@return.Expression is not null)
        {
            _ = VisitExpression(module, @return.Expression);
            // todo: Type check expression vs current function return type using TryCoerce.
        }
    }

    private void VisitIfElseStatement(ModuleDeclarationNode module, IfElseStatement ifElse)
    {
        _ = VisitExpression(module, ifElse.Condition);
        var conditionType = _typeMap.GetType(ifElse.Condition);
        if (conditionType is not BoolTypeNode)
        {
            _diagnostics.TypeMismatch(conditionType, PrimitiveTypeNames.boolean);
            return;
        }

        VisitBlock(module, ifElse.ThenBranch);

        if (ifElse.ElseBranch != null)
        {
            VisitBlock(module, ifElse.ElseBranch);
        }
    }

    private void VisitAssignmentStatement(ModuleDeclarationNode module, AssignmentStatementNode assignment)
    {
        assignment.Target = VisitExpression(module, assignment.Target);
        assignment.Value = VisitExpression(module, assignment.Value);

        var targetType = _typeMap.GetType(assignment.Target);

        if (!TryCoerce(module, assignment.Value, targetType, out var coercedValue))
        {
            var valueType = _typeMap.GetType(assignment.Value);
            _diagnostics.TypeMismatch(targetType, valueType);
            return;
        }

        assignment.Value = coercedValue;

        if (assignment.Target is VariableExpressionNode variable)
        {
            if (_scopedSymbolTable.TryGet(variable.Name, out var symbol) && !symbol.IsMutable)
            {
                _diagnostics.InvalidMutation(variable, symbol.Type.Span);
            }
        }
    }

    private ExpressionNode VisitExpression(ModuleDeclarationNode module, ExpressionNode expression)
    {
        return expression switch
        {
            UnaryExpressionNode unary => VisitUnaryExpression(module, unary),
            IntegerLiteralNode integerLiteral => VisitIntegerLiteralNode(integerLiteral),
            FloatLiteralNode floatLiteral => VisitFloatLiteralNode(floatLiteral),
            BooleanLiteralNode booleanLiteral => VisitBooleanLiteralNode(booleanLiteral),
            StructFieldInitializationExpressionNode structFieldInitializationExpression => VisitStructFieldInitializationExpression(module, structFieldInitializationExpression),
            VariableExpressionNode variableExpression => VisitVariableExpression(module, variableExpression),
            CallExpressionNode callExpression => VisitCallExpressionNode(module, callExpression),
            MemberAccessExpressionNode memberAccess => VisitMemberExpressionNode(module, memberAccess),
            BinaryExpressionNode binaryExpressionNode => VisitBinaryExpressionNode(module, binaryExpressionNode),
            AddressOfExpressionNode addressOf => VisitAddressOfExpression(module, addressOf),
            DereferenceExpressionNode dereference => VisitDereferenceExpressionNode(module, dereference),

            _ => expression
        };
    }

    private ExpressionNode VisitBooleanLiteralNode(BooleanLiteralNode booleanLiteral)
    {
        SetType(booleanLiteral, new BoolTypeNode(booleanLiteral.Span));
        return booleanLiteral;
    }

    private ExpressionNode VisitFloatLiteralNode(FloatLiteralNode floatLiteral)
    {
        var float32Type = new Float32TypeNode(floatLiteral.Span);
        FloatTypeNode floatType = TypeBounds.CanCoerceToType(floatLiteral.Value, float32Type)
            ? float32Type
            : new Float64TypeNode(floatLiteral.Span);
        SetType(floatLiteral, floatType);
        return floatLiteral;
    }

    private ExpressionNode VisitIntegerLiteralNode(IntegerLiteralNode integerLiteral)
    {
        var int32Type = new Int32TypeNode(integerLiteral.Span);
        SignedIntTypeNode intType = TypeBounds.CanCoerceToType(integerLiteral.Value, int32Type)
            ? int32Type
            : new Int64TypeNode(integerLiteral.Span);
        SetType(integerLiteral, intType);
        return integerLiteral;
    }

    private ExpressionNode VisitDereferenceExpressionNode(ModuleDeclarationNode module, DereferenceExpressionNode dereference)
    {
        dereference.Target = VisitExpression(module, dereference.Target);
        var targetType = _typeMap.GetType(dereference.Target);

        if (targetType is ReferenceTypeNode referenceTypeNode)
        {
            SetType(module, dereference, referenceTypeNode.Target);
        }
        else
        {
            _diagnostics.InvalidDereference(dereference, targetType);
        }

        return dereference;
    }

    private ExpressionNode VisitAddressOfExpression(ModuleDeclarationNode module, AddressOfExpressionNode addressOf)
    {
        addressOf.Target = VisitExpression(module, addressOf.Target);
        var targetType = _typeMap.GetType(addressOf.Target);

        var referenceType = new ReferenceTypeNode(targetType, addressOf.IsMutable, addressOf.Span);
        SetType(module, addressOf, referenceType);
        targetType = _typeMap.GetType(addressOf.Target);
        referenceType.Target = targetType;
        
        return addressOf;
    }

    private ExpressionNode VisitUnaryExpression(ModuleDeclarationNode module, UnaryExpressionNode unary)
    {
        unary.Operand = VisitExpression(module, unary.Operand);
        var operandType = _typeMap.GetType(unary.Operand);

        switch (unary.Operator)
        {
            case UnaryOperator.Negative:
            {
                if (operandType is not SignedIntTypeNode and not FloatTypeNode)
                {
                    _diagnostics.InvalidUnaryOperation(unary, operandType);
                }

                SetType(module, unary, operandType);
                return unary;
            }
            case UnaryOperator.Not:
            {
                if (operandType is not BoolTypeNode)
                {
                    _diagnostics.InvalidUnaryOperation(unary, operandType);
                }

                SetType(module, unary, new BoolTypeNode(unary.Span));
                return unary;
            }
            default:
                throw new ByronNotImplementedException(unary.Operator.ToString(), this, unary.Span);
        }
    }

    private ExpressionNode VisitStructFieldInitializationExpression(
        ModuleDeclarationNode module, 
        StructFieldInitializationExpressionNode initialization)
    {
        SetType(module, initialization, initialization.NominalType);
        var structType = _typeMap.GetType(initialization);
        initialization.NominalType = (NominalTypeNode)structType;
        
        var structName = initialization.NominalType.Symbol;
        
        if (!_globalSymbolTableLookup.TryResolveCanonicalType(initialization.NominalType, module.Symbol.Segments, module, out var typeCanonicalName))
        {
            _diagnostics.UndeclaredType(initialization.NominalType);
            return initialization;
        }
        
        var structDeclaration = _globalSymbolTableLookup.Structs[typeCanonicalName.Symbol];

        foreach (var fieldInitializer in initialization.FieldInitializers)
        {
            fieldInitializer.Value = VisitExpression(module, fieldInitializer.Value);

            var matchingField = structDeclaration.Fields.FirstOrDefault(f => f.Name == fieldInitializer.FieldName);
            if (matchingField is null)
            {
                _diagnostics.MissingMember(structType.Symbol.MemberName, fieldInitializer);
                continue;
            }

            if (!TryCoerce(module, fieldInitializer.Value, matchingField.Type, out var coercedValue))
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

    private ExpressionNode VisitBinaryExpressionNode(ModuleDeclarationNode module, BinaryExpressionNode binaryExpression)
    {
        binaryExpression.Left = VisitExpression(module, binaryExpression.Left);
        binaryExpression.Right = VisitExpression(module, binaryExpression.Right);

        var leftType = _typeMap.GetType(binaryExpression.Left);
        var rightType = _typeMap.GetType(binaryExpression.Right);

        if (binaryExpression.Operator.IsRelationalComparison())
        {
            var boolType = new BoolTypeNode(binaryExpression.Span);
            SetType(module, binaryExpression, boolType);

            if (TryGetPreferredCoercionType(binaryExpression.Left, leftType, binaryExpression.Right, rightType,
                    out var coercedType))
            {
                binaryExpression.Left = AddCastsWhenRequired(module, binaryExpression.Left, coercedType);
                binaryExpression.Right = AddCastsWhenRequired(module, binaryExpression.Right, coercedType);
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
            binaryExpression.Left = AddCastsWhenRequired(module, binaryExpression.Left, coerced);
            binaryExpression.Right = AddCastsWhenRequired(module, binaryExpression.Right, coerced);
            SetType(module, binaryExpression, coerced);
        }
        else
        {
            _diagnostics.TypeMismatch(leftType, rightType);
        }

        return binaryExpression;
    }

    private ExpressionNode AddCastsWhenRequired(ModuleDeclarationNode module, ExpressionNode expression, TypeNode targetType)
    {
        var sourceType = _typeMap.GetType(expression);

        if (sourceType.Symbol == targetType.Symbol)
        {
            return expression;
        }

        var cast = Cast(expression, sourceType, targetType);
        SetType(module, cast, targetType);
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

        if (leftType.Symbol == rightType.Symbol)
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

    private void VisitVariableDeclaration(ModuleDeclarationNode module, VariableDeclarationNode variableDeclaration)
    {
        if (_scopedSymbolTable.TryGet(variableDeclaration.Name, out _))
        {
            _diagnostics.Duplicate(variableDeclaration);
            _ = VisitExpression(module, variableDeclaration.Initializer);
            return;
        }

        variableDeclaration.Initializer = VisitExpression(module, variableDeclaration.Initializer);
        var inferredType = _typeMap.GetType(variableDeclaration.Initializer);
        var finalType = inferredType;

        if (variableDeclaration.TypeAnnotation is not null)
        {
            if (!_globalSymbolTableLookup.TypeExists(module, variableDeclaration.TypeAnnotation))
            {
                _diagnostics.UndeclaredType(variableDeclaration.TypeAnnotation);
                return;
            }

            if (!TryCoerce(module, variableDeclaration.Initializer, variableDeclaration.TypeAnnotation, out var coercedInitializer))
            {
                _diagnostics.TypeMismatch(variableDeclaration.TypeAnnotation, inferredType);
                return;
            }

            variableDeclaration.Initializer = coercedInitializer;
            variableDeclaration.TypeAnnotation = finalType;
        }

        _scopedSymbolTable.Declare(variableDeclaration.Name, finalType, variableDeclaration.IsMutable);
    }

    private ExpressionNode VisitVariableExpression(ModuleDeclarationNode module, VariableExpressionNode variableExpression)
    {
        if (_scopedSymbolTable.TryGet(variableExpression.Name, out var symbol))
        {
            SetType(module, variableExpression, symbol.Type);
        }
        else
        {
            _diagnostics.UndeclaredVariable(variableExpression);
        }

        return variableExpression;
    }
    
    private ExpressionNode VisitCallExpressionNode(ModuleDeclarationNode module, CallExpressionNode callExpression)
    {
        CallExpressionNode functionInvocation = callExpression;

        for (var i = 0; i < callExpression.Arguments.Count; i++)
        {
            callExpression.Arguments[i] = VisitExpression(module, callExpression.Arguments[i]);
        }

        string[] namespaceSegments;
        string functionName;

        if (callExpression.Callee is VariableExpressionNode variableExpression)
        {
            // todo: Work out what this lookup should _actually_ be
            if (_globalSymbolTableLookup.TryGetFunction(module, [], variableExpression.Name, module.Symbol.Segments, out var resolvedFunction))
            {
                functionName = resolvedFunction.Symbol.MemberName;
                namespaceSegments = resolvedFunction.Symbol.Segments[..^1];
                var functionInvocationNode = new FunctionInvocationVariableExpressionNode(resolvedFunction, variableExpression.Span);
                callExpression.Callee = functionInvocationNode;
            }
            else
            {
                functionName = variableExpression.Name;
                namespaceSegments = [];    
            }
        }
        else if (callExpression.Callee is MemberAccessExpressionNode memberAccess)
        {
            if (memberAccess.Target is VariableExpressionNode targetVariableExpression)
            {
                if(_globalSymbolTableLookup.TryGetStruct(module, targetVariableExpression.Name, module.Symbol.Segments, out var structDeclaration))
                {
                    namespaceSegments = structDeclaration.Symbol.Segments;
                    targetVariableExpression.Name = structDeclaration.Type.Symbol.ToString(); //todo: Is this correct? Should we prefer the same approach as if (callExpression.Callee is VariableExpressionNode variableExpression) where we use a new node type?

                    if (_globalSymbolTableLookup.TryGetFunction(module, namespaceSegments, memberAccess.MemberName,
                            module.Symbol.Segments, out var staticFunction))
                    {
                        var staticFunctionInvocation = new FunctionInvocationVariableExpressionNode(staticFunction, memberAccess.Span);
                        functionInvocation.Callee = staticFunctionInvocation;
                    }
                    
                }
                else if (_scopedSymbolTable.TryGet(targetVariableExpression.Name, out _))
                {
                    memberAccess.Target = VisitExpression(module, targetVariableExpression);
                    var memberAccessTargetType = _typeMap.GetType(memberAccess.Target);// todo: Should this be symbol.Type? 
                    namespaceSegments = memberAccessTargetType.Symbol.Segments;
                    
                    if (!_globalSymbolTableLookup.TryGetFunction(module, namespaceSegments, memberAccess.MemberName, module.Symbol.Segments, out var declaration))
                    {
                        _diagnostics.UndeclaredFunction(memberAccess.MemberName, callExpression.Callee.Span);
                        return functionInvocation;
                    }
                    
                    var functionInvocationNode = new FunctionInvocationVariableExpressionNode(declaration, memberAccess.Span);
                    functionInvocation = new MethodCallExpression(targetVariableExpression, functionInvocationNode, callExpression.Arguments, callExpression.Span);
                }
                else
                {
                    namespaceSegments = [targetVariableExpression.Name];
                }
            } 
            else
            {
                memberAccess.Target = VisitExpression(module, memberAccess.Target);

                if (_typeMap.TryGetType(memberAccess.Target, out var targetType))
                {
                    namespaceSegments = targetType.Symbol.Segments;
                    
                    if (!_globalSymbolTableLookup.TryGetFunction(module, namespaceSegments, memberAccess.MemberName, module.Symbol.Segments, out var declaration))
                    {
                        _diagnostics.UndeclaredFunction(memberAccess.MemberName, callExpression.Callee.Span);
                        return functionInvocation;
                    }
                    
                    var functionInvocationNode = new FunctionInvocationVariableExpressionNode(declaration, memberAccess.Span);
                    functionInvocation = new MethodCallExpression(memberAccess.Target, functionInvocationNode, callExpression.Arguments, callExpression.Span);
                }
                else
                {
                    namespaceSegments = [];
                }
            }

            functionName = memberAccess.MemberName;
        }
        else
        {
            throw new ByronNotImplementedException(callExpression.Callee.GetType(), this, callExpression.Span);
        }

        if (!_globalSymbolTableLookup.TryGetFunction(module, namespaceSegments, functionName, module.Symbol.Segments, out var function))
        {
            _diagnostics.UndeclaredFunction(functionName, callExpression.Callee.Span);
            return functionInvocation;
        }
        
        return functionInvocation switch
        {
            MethodCallExpression methodCall => TryCoerceAllArguments(module, methodCall, function),
            _ => TryCoerceAllArguments(module, functionInvocation, function)
        };
    }

    private MethodCallExpression TryCoerceAllArguments(ModuleDeclarationNode module, MethodCallExpression methodCall, FunctionDeclarationNode function)
    {
        if (!SupportsMethodInvocation(function.Signature))
        {
            _diagnostics.NoSelfArgument(function, methodCall.Span);
        }
        
        if (methodCall.Arguments.Count + 1 != function.Signature.Parameters.Count)
        {
            _diagnostics.InvalidArgumentCount(methodCall, function);
        }
                    
        var functionInvocationNode = new FunctionInvocationVariableExpressionNode(function, methodCall.Callee.Span);
        methodCall.Callee = functionInvocationNode;

        ExpressionNode[] arguments = [methodCall.Receiver, ..methodCall.Arguments];
        var maximumArgumentCount = Math.Min(arguments.Length, function.Signature.Parameters.Count);

        for (var i = 0; i < maximumArgumentCount; i++)
        {
            var argument = arguments[i];
            var argumentType = _typeMap.GetType(argument);

            if (i == 0)
            {
                var self = SelfParameter(function.Signature);
                var targetType = self.Type;
                
                if (!_globalSymbolTableLookup.TryResolveCanonicalType(self.Type, module.Symbol.Segments, module, out var resolvedSelfType))
                {
                    _diagnostics.UndeclaredType(self.Type, "self type argument");
                }
                else if(self.Ownership.IsReference())
                {
                    targetType = new ReferenceTypeNode(resolvedSelfType, self.Ownership.IsMutable(), self.Type.Span);
                }
                
                if (!TryCoerce(module, argument, targetType, out var coercedReceiver))
                {
                    _diagnostics.InvalidArgument(argumentType.Symbol, targetType.Symbol, function.Symbol, methodCall.Span);
                    return methodCall;
                }
                
                methodCall.Receiver = coercedReceiver;
            }
            else
            {
                
                var targetType = function.Signature.Parameters[i].Type;
                
                if (!TryCoerce(module, argument, targetType, out var coercedArgument))
                {
                    _diagnostics.InvalidArgument(argumentType.Symbol, targetType.Symbol, function.Symbol, methodCall.Span);
                    return methodCall;
                }
                methodCall.Arguments[i - 1] = coercedArgument;
            }
        }

        SetType(module, methodCall, function.Signature.ReturnType);
        return methodCall;
    }

    private CallExpressionNode TryCoerceAllArguments(ModuleDeclarationNode module, CallExpressionNode callExpression, FunctionDeclarationNode function)
    {
        if (callExpression.Arguments.Count != function.Signature.Parameters.Count)
        {
            _diagnostics.InvalidArgumentCount(callExpression, function);
        }

        var maximumArgumentCount = Math.Min(callExpression.Arguments.Count, function.Signature.Parameters.Count);

        for (var i = 0; i < maximumArgumentCount; i++)
        {
            var argument = callExpression.Arguments[i];
            var argumentType = _typeMap.GetType(argument);
            var parameterType = function.Signature.Parameters[i].Type;
        
            if (!TryCoerce(module, argument, parameterType, out var coercedExpression))
            {
                _diagnostics.InvalidArgument(argumentType.Symbol, parameterType.Symbol,
                    function.Symbol, callExpression.Span);
                return callExpression;
            }
        
            callExpression.Arguments[i] = coercedExpression;
        }

        SetType(module, callExpression, function.Signature.ReturnType);
        return callExpression;
    }
    
    private ExpressionNode VisitMemberExpressionNode(ModuleDeclarationNode module, MemberAccessExpressionNode memberAccess)
    {
        memberAccess.Target = VisitExpression(module, memberAccess.Target);
        var targetType = _typeMap.GetType(memberAccess.Target);
        
        if (_globalSymbolTableLookup.TryGetStruct(targetType, out var structDeclaration))
        {
            var field = structDeclaration.Fields.FirstOrDefault(f => f.Name == memberAccess.MemberName);
            if (field is not null)
            {
                SetType(module, memberAccess, field.Type);
            }
            else
            {
                _diagnostics.MissingMember(targetType.Symbol, memberAccess);
            }
        }
        else
        {
            _diagnostics.MissingMember(targetType.Symbol, memberAccess);
        }

        return memberAccess;
    }

    private bool TryCoerce(ModuleDeclarationNode module, ExpressionNode expression, TypeNode targetType,
        [NotNullWhen(true)] out ExpressionNode? result)
    {
        if (expression is AddressOfExpressionNode addressOf)
        {
            result = addressOf;
            return true;
        }

        var sourceType = _typeMap.GetType(expression);
        if (targetType is PrimitiveTypeNode p && sourceType.Symbol == p.Symbol)
        {
            result = expression;
            return true;
        }
        
        if (targetType is ReferenceTypeNode targetRef && sourceType.Symbol == targetRef.Target.Symbol)
        {
            result = new AddressOfExpressionNode(expression, targetRef.IsMutable, expression.Span);
            SetType(module, result, targetType);
            return true;
        }

        if (sourceType is ReferenceTypeNode sourceRef && sourceRef.Target.Symbol == targetType.Symbol)
        {
            result = new DereferenceExpressionNode(expression, expression.Span);
            SetType(module, result, targetType);
            return true;
        }
        
        if (_globalSymbolTableLookup.TryResolveCanonicalType(targetType, sourceType.Symbol.Segments, module, out var canonicalType))
        {
            if (canonicalType.Symbol == sourceType.Symbol)
            {
                SetType(module, expression, canonicalType);
                result = expression;
                return true;
            }
        }

        if (sourceType.Symbol.ToString() == targetType.Symbol.ToString())
        {
            result = expression;
            return true;
        }

        if (expression is IntegerLiteralNode intLiteral && targetType is NumericTypeNode targetNumeric)
        {
            if (TypeBounds.CanCoerceToType(intLiteral.Value, targetNumeric))
            {
                result = expression;
                SetType(intLiteral, targetType);
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
                SetType(floatLiteral, targetType);
                return true;
            }

            result = null;
            return false;
        }

        if (expression is StructFieldInitializationExpressionNode initialization)
        {
            if (sourceType is not NominalTypeNode nominalType)
            {
                _diagnostics.InvalidStructInitializationType(initialization.NominalType, initialization.Span);
                result = expression;
                return false;
            }

            SetType(initialization, nominalType);
            result = initialization;
            return true;
        }

        if (sourceType is IntegerTypeNode sourceInt && targetType is FloatTypeNode targetFloat)
        {
            var intToFloat = new CastIntToFloatNode(expression, targetFloat, sourceInt.Signed, expression.Span);
            result = intToFloat;
            SetType(intToFloat, targetType);
            return true;
        }

        if (sourceType is FloatTypeNode && targetType is IntegerTypeNode targetInt)
        {
            if (expression is FloatLiteralNode floatLit && !TypeBounds.CanCoerceToType(floatLit.Value, targetInt))
            {
                result = null;
                return false;
            }

            var floatToInt = new CastFloatToIntNode(expression, targetInt, targetInt.Signed, expression.Span);
            result = floatToInt;
            SetType(floatToInt, targetType);
            return true;
        }

        if (sourceType is IntegerTypeNode sourceIntToWiden && targetType is IntegerTypeNode widerInt &&
            sourceIntToWiden.BitWidth < widerInt.BitWidth)
        {
            var extendInteger = new ExtendIntegerNode(expression, widerInt, expression.Span);
            result = extendInteger;
            SetType(extendInteger, targetType);
            return true;
        }

        if (sourceType is FloatTypeNode sourceFloatToWiden && targetType is FloatTypeNode widerFloat &&
            sourceFloatToWiden.BitWidth < widerFloat.BitWidth)
        {
            var extendFloat = new ExtendFloatNode(expression, widerFloat, expression.Span);
            result = extendFloat;
            SetType(extendFloat, targetType);
            return true;
        }

        result = null;
        return false;
    }

    private void SetType(StructFieldInitializationExpressionNode expression, TypeNode targetType)
    {
        _typeMap.SetType(expression, targetType);
    }

    private void SetType(CastExpressionNode expression, TypeNode targetType)
    {
        _typeMap.SetType(expression, targetType);
    }

    private void SetType<T>(LiteralExpressionNode<T> expression, TypeNode targetType) where T : struct
    {
        _typeMap.SetType(expression, targetType);
    }

    private void SetType(ModuleDeclarationNode module, ExpressionNode expression, TypeNode possiblyUnresolvedType)
    {
        if (!_globalSymbolTableLookup.TryResolveCanonicalType(possiblyUnresolvedType, module.Symbol.Segments, module, out var resolvedType))
        {
            _diagnostics.UndeclaredType(possiblyUnresolvedType);
            return;
        }

        if (possiblyUnresolvedType is ReferenceTypeNode reference)
        {
            var canonicalReference = new ReferenceTypeNode(resolvedType, reference.IsMutable, reference.Span);
            _typeMap.SetType(expression, canonicalReference);
        }
        else
        {
            _typeMap.SetType(expression, resolvedType);
        }
        
    }
    
    bool SupportsMethodInvocation(FunctionSignatureNode signature) => signature.Parameters.Count > 0 && signature.Parameters[0].Name == ParameterNode.SelfArgumentName;
    
    
    public ParameterNode SelfParameter(FunctionSignatureNode signature)
    {
        if (!SupportsMethodInvocation(signature))
        {
            throw new InvalidOperationException($"Cannot get {ParameterNode.SelfArgumentName} argument for a function who doesn't support method invocation.");
        }
        return signature.Parameters[0];
        
    }
}