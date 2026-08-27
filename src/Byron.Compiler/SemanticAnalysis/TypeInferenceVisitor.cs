using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.SemanticAnalysis;

public class TypeInferenceVisitor(
    GlobalSymbolTableLookup globalSymbolTableLookup,
    CanonicalResolvingTypeMap typeMap,
    TypeCoercion typeCoercion,
    ScopedSymbolTable scopedSymbolTable,
    Diagnostics diagnostics)
{
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
        if (globalSymbolTableLookup.TryResolveCanonicalType(context.Module, type,
                context.ImplementBlock?.Symbol.Segments ?? context.Module.Symbol.Segments, out var lookup))
        {
            return lookup;
        }

        diagnostics.UndeclaredType(type, "function argument");
        return type;
    }

    public void VisitFunction(FunctionDeclarationNode function, FunctionDeclarationContext declarationContext)
    {
        scopedSymbolTable.EnterScope();

        TryCanonize(function.Signature, declarationContext);

        for (var i = 0; i < function.Signature.Parameters.Count; i++)
        {
            var parameter = function.Signature.Parameters[i];
            if (parameter.Name == ParameterNode.SelfArgumentName)
            {
                if (i != 0)
                {
                    diagnostics.InvalidSelfArgumentPosition(function, parameter);
                }

                if (declarationContext.ImplementBlock is null)
                {
                    diagnostics.InvalidSelfArgumentOutsideOfImplementBlock(function.Signature, parameter.Span);
                }
                else
                {
                    var expectedParameterType = declarationContext.ImplementBlock.TypeNode;
                    var actualParameterType = parameter.Type;

                    if (!globalSymbolTableLookup.TryResolveCanonicalType(declarationContext.Module, actualParameterType,
                            declarationContext.ImplementBlock.Symbol.Segments, out var canonicalType))
                    {
                        diagnostics.UndeclaredType(actualParameterType);
                    }

                    if (expectedParameterType == canonicalType)
                    {
                        parameter.Type = canonicalType;
                    }
                    else
                    {
                        diagnostics.InvalidSelfArgumentType(
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

            scopedSymbolTable.Declare(parameter.Name, type, isMutable);
        }

        VisitBlock(declarationContext.Module, function.Body);

        scopedSymbolTable.ExitScope();
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
        var conditionType = typeMap.GetType(ifElse.Condition);
        if (conditionType is not BoolTypeNode)
        {
            diagnostics.TypeMismatch(conditionType, PrimitiveTypeNames.boolean);
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

        var targetType = typeMap.GetType(assignment.Target);

        if (!typeCoercion.TryCoerce(module, assignment.Value, targetType, out var coercedValue))
        {
            var valueType = typeMap.GetType(assignment.Value);
            diagnostics.TypeMismatch(targetType, valueType);
            return;
        }

        assignment.Value = coercedValue;

        if (assignment.Target is VariableExpressionNode variable)
        {
            if (scopedSymbolTable.TryGet(variable.Name, out var symbol) && !symbol.IsMutable)
            {
                diagnostics.InvalidMutation(variable, symbol.Type.Span);
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
            StructFieldInitializationExpressionNode structFieldInitializationExpression =>
                VisitStructFieldInitializationExpression(module, structFieldInitializationExpression),
            VariableExpressionNode variableExpression => VisitVariableExpression(module, variableExpression),
            CallExpressionNode callExpression => VisitCallExpressionNode(module, callExpression),
            MemberAccessExpressionNode memberAccess => VisitMemberAccessExpressionNode(module, memberAccess),
            PathAccessExpressionNode pathAccess => VisitPathAccessExpressionNode(module, pathAccess),
            BinaryExpressionNode binaryExpressionNode => VisitBinaryExpressionNode(module, binaryExpressionNode),
            AddressOfExpressionNode addressOf => VisitAddressOfExpression(module, addressOf),
            DereferenceExpressionNode dereference => VisitDereferenceExpressionNode(module, dereference),

            _ => expression
        };
    }

    private ExpressionNode VisitBooleanLiteralNode(BooleanLiteralNode booleanLiteral)
    {
        typeMap.SetType(booleanLiteral, new BoolTypeNode(booleanLiteral.Span));
        return booleanLiteral;
    }

    private ExpressionNode VisitFloatLiteralNode(FloatLiteralNode floatLiteral)
    {
        var float32Type = new Float32TypeNode(floatLiteral.Span);
        FloatTypeNode floatType = TypeBounds.CanCoerceToType(floatLiteral.Value, float32Type)
            ? float32Type
            : new Float64TypeNode(floatLiteral.Span);
        typeMap.SetType(floatLiteral, floatType);
        return floatLiteral;
    }

    private ExpressionNode VisitIntegerLiteralNode(IntegerLiteralNode integerLiteral)
    {
        var int32Type = new Int32TypeNode(integerLiteral.Span);
        SignedIntTypeNode intType = TypeBounds.CanCoerceToType(integerLiteral.Value, int32Type)
            ? int32Type
            : new Int64TypeNode(integerLiteral.Span);
        typeMap.SetType(integerLiteral, intType);
        return integerLiteral;
    }

    private ExpressionNode VisitDereferenceExpressionNode(ModuleDeclarationNode module,
        DereferenceExpressionNode dereference)
    {
        dereference.Target = VisitExpression(module, dereference.Target);
        var targetType = typeMap.GetType(dereference.Target);

        if (targetType is ReferenceTypeNode referenceTypeNode)
        {
            typeMap.SetType(module, dereference, referenceTypeNode.Target);
        }
        else
        {
            diagnostics.InvalidDereference(dereference, targetType);
        }

        return dereference;
    }

    private ExpressionNode VisitAddressOfExpression(ModuleDeclarationNode module, AddressOfExpressionNode addressOf)
    {
        addressOf.Target = VisitExpression(module, addressOf.Target);
        var targetType = typeMap.GetType(addressOf.Target);

        var referenceType = new ReferenceTypeNode(targetType, addressOf.IsMutable, addressOf.Span);
        typeMap.SetType(module, addressOf, referenceType);
        targetType = typeMap.GetType(addressOf.Target);
        referenceType.Target = targetType;

        return addressOf;
    }

    private ExpressionNode VisitUnaryExpression(ModuleDeclarationNode module, UnaryExpressionNode unary)
    {
        unary.Operand = VisitExpression(module, unary.Operand);
        var operandType = typeMap.GetType(unary.Operand);

        switch (unary.Operator)
        {
            case UnaryOperator.Negative:
            {
                if (operandType is not SignedIntTypeNode and not FloatTypeNode)
                {
                    diagnostics.InvalidUnaryOperation(unary, operandType);
                }

                typeMap.SetType(module, unary, operandType);
                return unary;
            }
            case UnaryOperator.Not:
            {
                if (operandType is not BoolTypeNode)
                {
                    diagnostics.InvalidUnaryOperation(unary, operandType);
                }

                typeMap.SetType(module, unary, new BoolTypeNode(unary.Span));
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
        typeMap.SetType(module, initialization, initialization.NominalType);
        var structType = typeMap.GetType(initialization);
        initialization.NominalType = (NominalTypeNode)structType;

        if (!globalSymbolTableLookup.TryResolveCanonicalType(module, initialization.NominalType, module.Symbol.Segments,
                out var typeCanonicalName))
        {
            diagnostics.UndeclaredType(initialization.NominalType);
            return initialization;
        }

        var structDeclaration = globalSymbolTableLookup.Structs[typeCanonicalName.Symbol];

        foreach (var fieldInitializer in initialization.FieldInitializers)
        {
            fieldInitializer.Value = VisitExpression(module, fieldInitializer.Value);

            var matchingField = structDeclaration.Fields.FirstOrDefault(f => f.Name == fieldInitializer.FieldName);
            if (matchingField is null)
            {
                diagnostics.MissingMember(structType.Symbol.MemberName, fieldInitializer);
                continue;
            }

            if (!typeCoercion.TryCoerce(module, fieldInitializer.Value, matchingField.Type, out var coercedValue))
            {
                var valueType = typeMap.GetType(fieldInitializer.Value);
                diagnostics.TypeMismatch(matchingField.Type, valueType);
            }
            else
            {
                fieldInitializer.Value = coercedValue;
            }
        }

        return initialization;
    }

    private ExpressionNode VisitBinaryExpressionNode(ModuleDeclarationNode module,
        BinaryExpressionNode binaryExpression)
    {
        binaryExpression.Left = VisitExpression(module, binaryExpression.Left);
        binaryExpression.Right = VisitExpression(module, binaryExpression.Right);

        var leftType = typeMap.GetType(binaryExpression.Left);
        var rightType = typeMap.GetType(binaryExpression.Right);

        if (binaryExpression.Operator.IsRelationalComparison())
        {
            var boolType = new BoolTypeNode(binaryExpression.Span);
            typeMap.SetType(module, binaryExpression, boolType);

            if (typeCoercion.TryGetPreferredCoercionType(binaryExpression.Left, leftType, binaryExpression.Right,
                    rightType,
                    out var coercedType))
            {
                binaryExpression.Left = typeCoercion.AddCastsWhenRequired(module, binaryExpression.Left, coercedType);
                binaryExpression.Right = typeCoercion.AddCastsWhenRequired(module, binaryExpression.Right, coercedType);
            }
            else
            {
                diagnostics.TypeMismatch(leftType, rightType);
            }

            return binaryExpression;
        }

        if (typeCoercion.TryGetPreferredCoercionType(binaryExpression.Left, leftType, binaryExpression.Right, rightType,
                out var coerced))
        {
            binaryExpression.Left = typeCoercion.AddCastsWhenRequired(module, binaryExpression.Left, coerced);
            binaryExpression.Right = typeCoercion.AddCastsWhenRequired(module, binaryExpression.Right, coerced);
            typeMap.SetType(module, binaryExpression, coerced);
        }
        else
        {
            diagnostics.TypeMismatch(leftType, rightType);
        }

        return binaryExpression;
    }

    private void VisitVariableDeclaration(ModuleDeclarationNode module, VariableDeclarationNode variableDeclaration)
    {
        if (scopedSymbolTable.TryGet(variableDeclaration.Name, out _))
        {
            diagnostics.Duplicate(variableDeclaration);
            _ = VisitExpression(module, variableDeclaration.Initializer);
            return;
        }

        variableDeclaration.Initializer = VisitExpression(module, variableDeclaration.Initializer);
        var inferredType = typeMap.GetType(variableDeclaration.Initializer);
        var finalType = inferredType;

        if (variableDeclaration.TypeAnnotation is not null)
        {
            if (!globalSymbolTableLookup.TypeExists(module, variableDeclaration.TypeAnnotation))
            {
                diagnostics.UndeclaredType(variableDeclaration.TypeAnnotation);
                return;
            }

            if (!typeCoercion.TryCoerce(module, variableDeclaration.Initializer, variableDeclaration.TypeAnnotation,
                    out var coercedInitializer))
            {
                diagnostics.TypeMismatch(variableDeclaration.TypeAnnotation, inferredType);
                return;
            }

            variableDeclaration.Initializer = coercedInitializer;
            variableDeclaration.TypeAnnotation = finalType;
        }

        scopedSymbolTable.Declare(variableDeclaration.Name, finalType, variableDeclaration.IsMutable);
    }

    private ExpressionNode VisitVariableExpression(ModuleDeclarationNode module,
        VariableExpressionNode variableExpression)
    {
        if (scopedSymbolTable.TryGet(variableExpression.Name, out var symbol))
        {
            typeMap.SetType(module, variableExpression, symbol.Type);
        }
        else
        {
            diagnostics.UndeclaredVariable(variableExpression);
        }

        return variableExpression;
    }

    private ExpressionNode VisitPathAccessExpressionNode(ModuleDeclarationNode module,
        PathAccessExpressionNode pathAccess)
    {
        var candidateType = new NominalTypeNode(pathAccess.Path, pathAccess.Span);
        if (globalSymbolTableLookup.TryResolveCanonicalType(module, candidateType, module.Symbol.Segments,
                out var resolvedType))
        {
            typeMap.SetType(pathAccess, resolvedType);
            return pathAccess;
        }

        if (scopedSymbolTable.TryGet(pathAccess.IdentifierSegments[0].Name, out var resolvedSymbol))
        {
            typeMap.SetType(pathAccess, resolvedSymbol.Type);
        }

        return pathAccess;
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
            if (globalSymbolTableLookup.TryGetFunction(
                    module, 
                    Symbol.From(variableExpression.Name),
                    // [], 
                    variableExpression.Name, 
                    module.Symbol.Segments, 
                    out var resolvedFunction))
            {
                functionName = resolvedFunction.Symbol.MemberName;
                namespaceSegments = resolvedFunction.Symbol.Path;
                var functionInvocationNode =
                    new FunctionInvocationVariableExpressionNode(resolvedFunction, variableExpression.Span);
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
            if (memberAccess.Target is PathAccessExpressionNode pathAccessExpression)
            {
                if (globalSymbolTableLookup.TryGetFunction(
                        module,
                        Symbol.From([..pathAccessExpression.Path, memberAccess.MemberName]),
                        // [],
                        memberAccess.MemberName,
                        pathAccessExpression.Path,
                        out var associatedFreeFunction))
                {
                    var associatedFunctionInvokation =
                        new FunctionInvocationVariableExpressionNode(associatedFreeFunction, memberAccess.Span);
                    functionInvocation.Callee = associatedFunctionInvokation;
                }

                namespaceSegments = pathAccessExpression.Path;
            }
            else if (memberAccess.Target is VariableExpressionNode targetVariableExpression)
            {
                if (globalSymbolTableLookup.TryGetStruct(module, targetVariableExpression.Name, module.Symbol.Segments,
                        out var structDeclaration))
                {
                    namespaceSegments = structDeclaration.Symbol.Segments;
                    targetVariableExpression.Name = structDeclaration.Type.Symbol.ToString();

                    if (globalSymbolTableLookup.TryGetFunction(
                            module,
                            Symbol.From([..namespaceSegments, memberAccess.MemberName]),
                            // namespaceSegments,
                            memberAccess.MemberName,
                            module.Symbol.Segments,
                            out var staticFunction))
                    {
                        var staticFunctionInvocation =
                            new FunctionInvocationVariableExpressionNode(staticFunction, memberAccess.Span);
                        functionInvocation.Callee = staticFunctionInvocation;
                    }
                }
                else if (scopedSymbolTable.TryGet(targetVariableExpression.Name, out _))
                {
                    memberAccess.Target = VisitExpression(module, targetVariableExpression);
                    var memberAccessTargetType = typeMap.GetType(memberAccess.Target);
                    namespaceSegments = memberAccessTargetType.Symbol.Segments;

                    if (!globalSymbolTableLookup.TryGetFunction(
                            module,
                            Symbol.From([..namespaceSegments, memberAccess.MemberName]),
                            // namespaceSegments, 
                            memberAccess.MemberName,
                            module.Symbol.Segments, 
                            out var declaration))
                    {
                        diagnostics.UndeclaredFunction(memberAccess.MemberName, callExpression.Callee.Span);
                        return functionInvocation;
                    }

                    var functionInvocationNode =
                        new FunctionInvocationVariableExpressionNode(declaration, memberAccess.Span);
                    functionInvocation = new MethodCallExpression(targetVariableExpression, functionInvocationNode,
                        callExpression.Arguments, callExpression.Span);
                }
                else
                {
                    namespaceSegments = [targetVariableExpression.Name];
                }
            }
            else
            {
                memberAccess.Target = VisitExpression(module, memberAccess.Target);

                if (typeMap.TryGetType(memberAccess.Target, out var targetType))
                {
                    namespaceSegments = targetType.Symbol.Segments;

                    if (!globalSymbolTableLookup.TryGetFunction(
                            module,
                            Symbol.From([..namespaceSegments, memberAccess.MemberName]),
                            // namespaceSegments, 
                            memberAccess.MemberName,
                            module.Symbol.Segments, 
                            out var declaration))
                    {
                        diagnostics.UndeclaredFunction(memberAccess.MemberName, callExpression.Callee.Span);
                        return functionInvocation;
                    }

                    var functionInvocationNode =
                        new FunctionInvocationVariableExpressionNode(declaration, memberAccess.Span);
                    functionInvocation = new MethodCallExpression(memberAccess.Target, functionInvocationNode,
                        callExpression.Arguments, callExpression.Span);
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

        if (!globalSymbolTableLookup.TryGetFunction(
                module,
                Symbol.From([..namespaceSegments, functionName]),
                // namespaceSegments, 
                functionName, 
                module.Symbol.Segments,
                out var function))
        {
            diagnostics.UndeclaredFunction(functionName, callExpression.Callee.Span);
            return functionInvocation;
        }

        return functionInvocation switch
        {
            MethodCallExpression methodCall => TryCoerceAllArguments(module, methodCall, function),
            _ => TryCoerceAllArguments(module, functionInvocation, function)
        };
    }

    private MethodCallExpression TryCoerceAllArguments(ModuleDeclarationNode module, MethodCallExpression methodCall,
        FunctionDeclarationNode function)
    {
        if (!SupportsMethodInvocation(function.Signature))
        {
            diagnostics.NoSelfArgument(function, methodCall.Span);
        }

        if (methodCall.Arguments.Count + 1 != function.Signature.Parameters.Count)
        {
            diagnostics.InvalidArgumentCount(methodCall, function);
        }

        var functionInvocationNode = new FunctionInvocationVariableExpressionNode(function, methodCall.Callee.Span);
        methodCall.Callee = functionInvocationNode;

        ExpressionNode[] arguments = [methodCall.Receiver, ..methodCall.Arguments];
        var maximumArgumentCount = Math.Min(arguments.Length, function.Signature.Parameters.Count);

        for (var i = 0; i < maximumArgumentCount; i++)
        {
            var argument = arguments[i];
            var argumentType = typeMap.GetType(argument);

            if (i == 0)
            {
                var self = SelfParameter(function.Signature);
                var targetType = self.Type;

                // if (!globalSymbolTableLookup.TryResolveCanonicalType(self.Type, module.Symbol.Segments, module, out var resolvedSelfType))
                if (!globalSymbolTableLookup.TryResolveCanonicalType(module, self.Type, function.Symbol.Path,
                        out var resolvedSelfType))
                {
                    diagnostics.UndeclaredType(self.Type, "self type argument");
                }
                else if (self.Ownership.IsReference())
                {
                    targetType = new ReferenceTypeNode(resolvedSelfType, self.Ownership.IsMutable(), self.Type.Span);
                }

                if (!typeCoercion.TryCoerce(module, argument, targetType, out var coercedReceiver))
                {
                    diagnostics.InvalidArgument(argumentType.Symbol, targetType.Symbol, function.Symbol,
                        methodCall.Span);
                    return methodCall;
                }

                methodCall.Receiver = coercedReceiver;
            }
            else
            {
                var targetType = function.Signature.Parameters[i].Type;

                if (!typeCoercion.TryCoerce(module, argument, targetType, out var coercedArgument))
                {
                    diagnostics.InvalidArgument(argumentType.Symbol, targetType.Symbol, function.Symbol,
                        methodCall.Span);
                    return methodCall;
                }

                methodCall.Arguments[i - 1] = coercedArgument;
            }
        }

        typeMap.SetType(module, methodCall, function.Signature.ReturnType);
        return methodCall;
    }

    private CallExpressionNode TryCoerceAllArguments(ModuleDeclarationNode module, CallExpressionNode callExpression,
        FunctionDeclarationNode function)
    {
        if (callExpression.Arguments.Count != function.Signature.Parameters.Count)
        {
            diagnostics.InvalidArgumentCount(callExpression, function);
        }

        var maximumArgumentCount = Math.Min(callExpression.Arguments.Count, function.Signature.Parameters.Count);

        for (var i = 0; i < maximumArgumentCount; i++)
        {
            var argument = callExpression.Arguments[i];
            var argumentType = typeMap.GetType(argument);
            var parameterType = function.Signature.Parameters[i].Type;

            if (!typeCoercion.TryCoerce(module, argument, parameterType, out var coercedExpression))
            {
                diagnostics.InvalidArgument(argumentType.Symbol, parameterType.Symbol,
                    function.Symbol, callExpression.Span);
                return callExpression;
            }

            callExpression.Arguments[i] = coercedExpression;
        }

        typeMap.SetType(module, callExpression, function.Signature.ReturnType);
        return callExpression;
    }

    private ExpressionNode VisitMemberAccessExpressionNode(ModuleDeclarationNode module,
        MemberAccessExpressionNode memberAccess)
    {
        memberAccess.Target = VisitExpression(module, memberAccess.Target);
        var targetType = typeMap.GetType(memberAccess.Target);

        if (globalSymbolTableLookup.TryGetStruct(targetType, out var structDeclaration))
        {
            var field = structDeclaration.Fields.FirstOrDefault(f => f.Name == memberAccess.MemberName);
            if (field is not null)
            {
                typeMap.SetType(module, memberAccess, field.Type);
            }
            else
            {
                diagnostics.MissingMember(targetType.Symbol, memberAccess);
            }
        }
        else
        {
            diagnostics.MissingMember(targetType.Symbol, memberAccess);
        }

        return memberAccess;
    }

    bool SupportsMethodInvocation(FunctionSignatureNode signature) => signature.Parameters.Count > 0 &&
                                                                      signature.Parameters[0].Name ==
                                                                      ParameterNode.SelfArgumentName;

    private ParameterNode SelfParameter(FunctionSignatureNode signature)
    {
        if (!SupportsMethodInvocation(signature))
        {
            throw new ByronSemanticAnalysisException(
                $"Cannot get {ParameterNode.SelfArgumentName} argument for a function who doesn't support method invocation.",
                diagnostics);
        }

        return signature.Parameters[0];
    }
}