using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
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
        _functionRegistry =  functionRegistry;
        _typeMap = typeMap;
        _symbolTable = symbolTable;
        _diagnostics = diagnostics;
    }

    public void VisitFunction(FunctionDeclarationNode function)
    {
        _symbolTable.EnterScope();

        foreach (var parameter in function.Parameters)
        {
            var isMutable = parameter.Ownership is ReceiverBindingOwnership.MutableBorrow or ReceiverBindingOwnership.Owned;
            _symbolTable.Declare(parameter.Name, parameter.Type, isMutable);
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
                VisitExpression(expressionStatement.Expression);
                break;
            case AssignmentStatementNode assignment:
                VisitAssignmentStatement(assignment);
                break;
            case  IfElseStatement ifElse:
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

    private void VisitExpression(ExpressionNode expression)
    {
        switch (expression)
        {
            case UnaryExpressionNode unary:   
                VisitUnaryExpression(unary);
                break;
            
            case IntegerLiteralNode integerLiteral:
                var int32Type = new Int32TypeNode(integerLiteral.Span);
                SignedIntTypeNode intType = TypeBounds.CanCoerceToType(integerLiteral.Value, int32Type)
                    ? int32Type
                    : new Int64TypeNode(integerLiteral.Span);
                _typeMap.SetType(integerLiteral, intType); 
                break;
            
            case FloatLiteralNode floatLiteral:
                var float32Type = new Float32TypeNode(floatLiteral.Span);
                FloatTypeNode floatType = TypeBounds.CanCoerceToType(floatLiteral.Value, float32Type)
                    ? float32Type
                    : new Float64TypeNode(floatLiteral.Span);
                _typeMap.SetType(floatLiteral, floatType);
                break;
            
            case BoolLiteralNode boolLiteral:
                _typeMap.SetType(boolLiteral, new BoolTypeNode(boolLiteral.Span));
                break;    
            
            case StructFieldInitializationExpressionNode structFieldInitializationExpression:
                VisitStructFieldInitializationExpression(structFieldInitializationExpression);
                break;

            case VariableExpressionNode variableExpression:
                VisitVariableExpression(variableExpression);
                break;
            
            case CallExpressionNode callExpression:
                VisitCallExpressionNode(callExpression);
                break;

            case MemberAccessExpressionNode memberAccess:
                VisitMemberExpressionNode(memberAccess);
                break;
            
            case BinaryExpressionNode binaryExpressionNode:
                VisitBinaryExpressionNode(binaryExpressionNode);
                break;
        }
        
        // throw new ByronNotImplementedException(expression.GetType(), this, expression.Span);
    }

    private void VisitUnaryExpression(UnaryExpressionNode unary)
    {
        VisitExpression(unary.Operand);
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
                return;
            }
            case UnaryOperator.Not:
            {
                if (operandType is not BoolTypeNode)
                {
                    _diagnostics.InvalidUnaryOperation(unary, operandType);
                }
                _typeMap.SetType(unary, new BoolTypeNode(unary.Span));
                return;
            }
            default:
                throw new ByronNotImplementedException(unary.Operator.ToString(), this, unary.Span);
        }
    }

    private void VisitStructFieldInitializationExpression(StructFieldInitializationExpressionNode initialization)
    {
        _typeMap.SetType(initialization, initialization.NominalType);
        var structName = initialization.NominalType.CanonicalName();
        if (!_typeRegistry.TryGetStruct(structName, out var structDeclaration))
        {
            _diagnostics.UndeclaredType(initialization.NominalType);
            return;
        }
        
        foreach (var fieldInitializer in initialization.FieldInitializers)
        {
            VisitExpression(fieldInitializer.Value);
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
    }

    private void VisitBinaryExpressionNode(BinaryExpressionNode binaryExpression)
    {
        VisitExpression(binaryExpression.Left);
        VisitExpression(binaryExpression.Right);
        
        var leftType = _typeMap.GetType(binaryExpression.Left);
        var rightType = _typeMap.GetType(binaryExpression.Right);
        
        if (binaryExpression.Operator.IsRelationalComparison())
        {
            var boolType = new BoolTypeNode(binaryExpression.Span);
            _typeMap.SetType(binaryExpression, boolType);
            
            if (TryGetPreferredCoercionType(binaryExpression.Left, leftType, binaryExpression.Right, rightType, out var coercedType))
            {
                binaryExpression.Left = AddCastsWhenRequired(binaryExpression.Left, coercedType); 
                binaryExpression.Right = AddCastsWhenRequired(binaryExpression.Right, coercedType);
            }
            else
            {
                _diagnostics.TypeMismatch(leftType, rightType);
            }
            
            return;
        }

        if (TryGetPreferredCoercionType(binaryExpression.Left, leftType, binaryExpression.Right, rightType, out var coerced))
        {
            binaryExpression.Left = AddCastsWhenRequired(binaryExpression.Left, coerced);
            binaryExpression.Right = AddCastsWhenRequired(binaryExpression.Right, coerced);
            _typeMap.SetType(binaryExpression, coerced);
        }
        else
        {
            _diagnostics.TypeMismatch(leftType, rightType);
        }
    }

    private ExpressionNode AddCastsWhenRequired(ExpressionNode expression, TypeNode targetType)
    {
        var sourceType = _typeMap.GetType(expression);

        if (sourceType.CanonicalName() == targetType.CanonicalName())
        {
            return expression;
        }

        if (TryCreateCastNode(expression, sourceType, targetType, out var castNode))
        {
            _typeMap.SetType(castNode, targetType);
            return castNode;
        }

        return expression;
    }
    
    private bool TryCreateCastNode(ExpressionNode operand, TypeNode sourceType, TypeNode targetType, [NotNullWhen(true)] out CastExpressionNode? castNode)
    {
        castNode = null;

        if (sourceType is IntegerTypeNode sourceInt && targetType is FloatTypeNode targetFloat)
        {
            castNode = new CastIntToFloatNode(operand, targetFloat, sourceInt.Signed, operand.Span);
            return true;
        }

        if (sourceType is FloatTypeNode && targetType is IntegerTypeNode targetInt)
        {
            if (operand is FloatLiteralNode floatLit && !TypeBounds.CanCoerceToType(floatLit.Value, targetInt))
            {
                return false;
            }

            castNode = new CastFloatToIntNode(operand, targetInt, targetInt.Signed, operand.Span);
            return true;
        }

        if (sourceType is IntegerTypeNode sourceIntToWiden && targetType is IntegerTypeNode widerInt)
        {
            if (sourceIntToWiden.BitWidth < widerInt.BitWidth)
            {
                castNode = new ExtendIntegerNode(operand, widerInt, operand.Span);
                return true;
            }

            return false;
        }

        if (sourceType is FloatTypeNode sourceFloatToWiden && targetType is FloatTypeNode widerFloat && sourceFloatToWiden.BitWidth < widerFloat.BitWidth)
        {
            castNode = new ExtendFloatNode(operand, widerFloat, operand.Span);
            return true;
        }

        return false;
    }
    
    private bool TryGetPreferredCoercionType(ExpressionNode leftExpression, TypeNode leftType, ExpressionNode rightExpression, TypeNode rightType, [NotNullWhen(true)] out TypeNode? preferredType)
    {
        preferredType = null;
        
        if (leftType.CanonicalName() == rightType.CanonicalName())
        {
            preferredType = leftType;
            return true;
        }
        
        if (leftType is IntegerTypeNode integerLeft && rightType is FloatTypeNode && TypeBounds.CanCoerceToType(rightExpression, integerLeft))
        {
            preferredType = leftType;
            return true;
        }

        if (rightType is IntegerTypeNode integerRight && leftType is FloatTypeNode && TypeBounds.CanCoerceToType(leftExpression, integerRight))
        {
            preferredType = rightType;
            return true;
        }

        // if (TryPromoteNumericTypes(leftType, rightType, out var promotedType))
        // {
        //     preferredType = promotedType;
        //     return true;
        // }
        
        return false;
    }

    // private bool TryPromoteNumericTypes(TypeNode leftType, TypeNode rightType, [NotNullWhen(true)] out TypeNode? promotedType)
    // {
    //     throw new ByronNotImplementedException("TryPromoteNumericTypes", this);
    // }

    private void VisitVariableDeclaration(VariableDeclarationNode variableDeclaration)
    {
        if (_symbolTable.TryGet(variableDeclaration.Name, out _))
        {
            _diagnostics.Duplicate(variableDeclaration);
            VisitExpression(variableDeclaration.Initializer);
            return;
        }
        
        VisitExpression(variableDeclaration.Initializer);
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
        var declaredTypeName = declaredType.CanonicalName();
        var assignedValueTypeName = assignedValueType.CanonicalName();

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
    
    private bool IsAssignable(ExpressionNode value, TypeNode assignedType, TypeNode targetType)
    {
        if (assignedType.CanonicalName() == targetType.CanonicalName())
        {
            return true;
        }

        if (assignedType is not NumericTypeNode assignedNumeric || targetType is not NumericTypeNode targetNumeric)
        {
            return false;
        }

        if (value is IntegerLiteralNode intLiteral && TypeBounds.CanCoerceToType(intLiteral.Value, targetNumeric))
        {
            return true;
        }
        if (value is FloatLiteralNode floatLiteral && TypeBounds.CanCoerceToType(floatLiteral.Value, targetNumeric))
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

    private bool IsAssignable(AssignmentStatementNode assignment, TypeNode assignedValueType)
    {
        var targetType = _typeMap.GetType(assignment.Target);
        var declaredTypeName = targetType.CanonicalName();
        var assignedValueTypeName = assignedValueType.CanonicalName();

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

    private void VisitVariableExpression(VariableExpressionNode variableExpression)
    {
        if (_symbolTable.TryGet(variableExpression.Name, out var symbol))
        {
            _typeMap.SetType(variableExpression, symbol.Type);
        }
        else
        {
            _diagnostics.UndeclaredVariable(variableExpression);
        }
    }

    private void VisitCallExpressionNode(CallExpressionNode callExpression)
    {
        foreach (var argument in callExpression.Arguments)
        {
            VisitExpression(argument);
        }

        if (callExpression.Callee is not VariableExpressionNode variableExpression)
        {
            throw new ByronNotImplementedException("Complex callee expressions", this, callExpression.Span);
        }
        
        if (!_functionRegistry.TryGetFunctionInScope([], variableExpression.Name, out var function))
        {
            _diagnostics.UndeclaredFunction(variableExpression);
            return;
        }

        if (function.Parameters.Count != callExpression.Arguments.Count)
        {
            _diagnostics.InvalidArgumentCount(callExpression, function);
        }

        var maximumArgumentCount = Math.Min(callExpression.Arguments.Count, function.Parameters.Count);
        
        for (var i = 0; i < maximumArgumentCount; i++)
        {
            var expression = callExpression.Arguments[i];
            var argumentType = _typeMap.GetType(expression);
            var parameterType = function.Parameters[i].Type;

            if (!IsAssignable(expression, argumentType, parameterType))
            {
                _diagnostics.InvalidArgument(argumentType.CanonicalName(), parameterType.CanonicalName(), callExpression.Span);
                return;
            }
        }
        
        _typeMap.SetType(callExpression, function.ReturnType);
    }

    private void VisitMemberExpressionNode(MemberAccessExpressionNode memberAccess)
    {
        VisitExpression(memberAccess.Target);
        var targetType = _typeMap.GetType(memberAccess.Target);

        var targetTypeCanonicalName = targetType.CanonicalName();
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
    }
}