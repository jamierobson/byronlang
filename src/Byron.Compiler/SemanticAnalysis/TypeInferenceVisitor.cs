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
        }
    }

    private void VisitAssignmentStatement(AssignmentStatementNode assignment)
    {
        VisitExpression(assignment.Target);
        VisitExpression(assignment.Value);
        
        var targetType = _typeMap.GetType(assignment.Target);
        var valueType = _typeMap.GetType(assignment.Value);
        
        if (targetType.CanonicalName() != valueType.CanonicalName())
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
            case IntegerLiteralNode integerLiteral:
                _typeMap.SetType(integerLiteral, new Int32TypeNode(integerLiteral.Span));
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

            if (valueType.CanonicalName() != matchingField.Type.CanonicalName())
            {
                _diagnostics.TypeMismatch(matchingField.Type, valueType);
            }
        }
    }

    private void VisitBinaryExpressionNode(BinaryExpressionNode binaryExpression)
    {
        VisitExpression(binaryExpression.Left);
        VisitExpression(binaryExpression.Right);
        
        var left = _typeMap.GetType(binaryExpression.Left);
        var right = _typeMap.GetType(binaryExpression.Right);

        if (left.CanonicalName() != right.CanonicalName())
        {
            _diagnostics.TypeMismatch(right, left);
            return;
        }
        
        _typeMap.SetType(binaryExpression, left);
    }

    private void VisitVariableDeclaration(VariableDeclarationNode variableDeclaration)
    {
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

            if (variableDeclaration.TypeAnnotation.CanonicalName() != inferredType.CanonicalName())
            {
                _diagnostics.TypeMismatch(variableDeclaration.TypeAnnotation, inferredType);
                return;
            }

            finalType = variableDeclaration.TypeAnnotation;
        }

        _symbolTable.Declare(variableDeclaration.Name, finalType, variableDeclaration.IsMutable);
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

        for (var i = 0; i < callExpression.Arguments.Count; i++)
        {
            var argumentType = _typeMap.GetType(callExpression.Arguments[i]);
            var parameterType = function.Parameters[i].Type;

            if (argumentType.CanonicalName() != parameterType.CanonicalName())
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