using Byron.Compiler.Exceptions;
using Byron.Compiler.SemanticAnalysis;
using High = Byron.Compiler.AST.HighLevel;
using Low = Byron.Compiler.AST.LowLevel;

namespace Byron.Compiler.Parser;

public class ByronLoweringPass
{
    
    private readonly High.ProgramNode _ast;
    private readonly TypeRegistry _typeRegistry;
    private readonly TypeMap _typeMap;
    private readonly FunctionRegistry _functionRegistry;
    
    public ByronLoweringPass(SemanticAnalysisResult semanticAnalysisResult)
    {
        if (!semanticAnalysisResult.Success)
        {
            throw new ByronLowLevelParserException("Unable to lower an invalid AST");
        }
        
        (_ast, _typeRegistry, _typeMap, _functionRegistry) = semanticAnalysisResult;
    }
    
    public Low.ProgramNode Lower()
    {
        
        var declarations = _ast.Declarations
        .Select(TopLevelDeclaration)
        .ToList();

        return new Low.ProgramNode(declarations);
    }

    private Low.TopLevelDeclarationNode TopLevelDeclaration(High.TopLevelDeclarationNode declaration)
    {
        return declaration switch
        {
            High.FunctionDeclarationNode function => FunctionDeclaration(function),
            High.StructDeclarationNode @struct => StructDeclaration(@struct),
            _ => throw new ByronNotImplementedException(declaration.GetType(), this, declaration.Span) 
        };
    }

    private Low.StructDeclarationNode StructDeclaration(High.StructDeclarationNode @struct)
    {
        var fields = @struct.Fields.Select(x => new Low.StructFieldNode(x.Name, Type(x.Type))).ToList();
        return new Low.StructDeclarationNode(@struct.Name, fields);
    } 

    private Low.FunctionDeclarationNode FunctionDeclaration(High.FunctionDeclarationNode declaration)
    {
        var parameters = declaration.Parameters.Select(Parameter).ToList();
        var returnType = Type(declaration.ReturnType);
        var body = BlockStatement(declaration.Body);

        return new Low.FunctionDeclarationNode(declaration.Name, parameters, returnType, body);
    }

    private Low.ParameterNode Parameter(High.ParameterNode parameter)
    {
        var type = Type(parameter.Type);
        return new Low.ParameterNode(parameter.Ownership, parameter.Name, type);
    }

    private Low.TypeNode Type(High.TypeNode type)
    {
        return type switch
        {
            High.ReferenceTypeNode referenceType => new Low.ReferenceTypeNode(Type(referenceType.Target), referenceType.IsMutable),
            High.NominalTypeNode userDeclaredType => new Low.NominalTypeNode(userDeclaredType.CanonicalName()),
            
            High.VoidTypeNode => new Low.VoidTypeNode(),
            
            
            High.SignedIntTypeNode signed => new Low.SignedIntTypeNode(signed.BitWidth),
            High.UnsignedIntTypeNode unsigned => new Low.UnsignedIntTypeNode(unsigned.BitWidth),
            High.FloatTypeNode @float => new Low.FloatTypeNode(@float.BitWidth),
            
            // High.Int8TypeNode => new Low.Int8TypeNode(),
            // High.Int16TypeNode => new Low.Int16TypeNode(),
            // High.Int32TypeNode => new Low.Int32TypeNode(),
            // High.Int64TypeNode => new Low.Int64TypeNode(),
            // High.UInt8TypeNode => new Low.UInt8TypeNode(),
            // High.UInt16TypeNode => new Low.UInt16TypeNode(),
            // High.UInt32TypeNode => new Low.UInt32TypeNode(),
            // High.UInt64TypeNode => new Low.UInt64TypeNode(),
            // High.Float32TypeNode => new Low.Float32TypeNode(),
            // High.Float64TypeNode => new Low.Float64TypeNode(),
            High.BoolTypeNode => new Low.BoolTypeNode(),
            High.RuneTypeNode => new Low.RuneTypeNode(),

            _ => throw new ByronNotImplementedException(type.GetType(), this, type.Span)
        };
    }

    private Low.StatementNode Statement(High.StatementNode statement)
    {
        return statement switch
        {
            High.BlockStatementNode block => BlockStatement(block),
            High.ReturnStatementNode @return => new Low.ReturnStatementNode(@return.Expression != null ? Expression(@return.Expression) : null),
            High.YieldStatementNode yield => new Low.YieldStatementNode(Expression(yield.Expression)),
            High.DiscardStatementNode discard => new Low.DiscardStatementNode(Expression(discard.Initializer)),
            High.VariableDeclarationNode variable => Variable(variable),
            High.IfElseStatement ifElse => IfElse(ifElse),
            High.BreakStatement _ => new Low.BreakStatement(),
            High.ContinueStatement _ => new Low.ContinueStatement(),
            High.WhileStatement @while => new Low.WhileStatement(Expression(@while.ContinuationCondition), BlockStatement(@while.Body)),
            High.AssignmentStatementNode assignment => new Low.AssignmentStatementNode(Expression(assignment.Target), Expression(assignment.Value)),
            _ => throw new ByronNotImplementedException(statement.GetType(), this, statement.Span)
        };
    }

    private Low.ExpressionNode Expression(High.ExpressionNode expression)
    {
        return expression switch
        {
            High.FloatLiteralNode floatLiteral => new Low.FloatLiteralNode(floatLiteral.Value),
            High.IntegerLiteralNode intLiteral => new Low.IntegerLiteralNode(intLiteral.Value),
            High.BoolLiteralNode boolLiteral => new Low.BoolLiteralNode(boolLiteral.Value),
            High.VariableExpressionNode variable => new Low.VariableExpressionNode(variable.Name),
            High.CallExpressionNode call => CallExpression(call),
            High.BinaryExpressionNode binary => CoercedBinaryExpression(binary), 
            High.StructFieldInitializationExpressionNode structFieldInitialization => StructFieldInitializationExpression(structFieldInitialization),
            High.MemberAccessExpressionNode memberAccess => new Low.MemberAccessExpressionNode(Expression(memberAccess.Target), memberAccess.MemberName),
            High.CastFloatToIntNode floatToInt => new Low.CastFloatToIntNode(Expression(floatToInt.Operand), Type(floatToInt.TargetType), floatToInt.IsSigned), 
            High.CastIntToFloatNode intToFloat => new Low.CastIntToFloatNode(Expression(intToFloat.Operand), Type(intToFloat.TargetType), intToFloat.IsSigned),
            High.ExtendIntegerNode extendInt => new Low.ExtendIntegerNode(Expression(extendInt.Operand), Type(extendInt.TargetType)),
            High.ExtendFloatNode extendFloat => new Low.ExtendFloatNode(Expression(extendFloat.Operand), Type(extendFloat.TargetType)),
            
            // Lowerable expressions here

            _ => throw new ByronNotImplementedException(expression.GetType(), this, expression.Span)
        };
    }

    private Low.BinaryExpressionNode CoercedBinaryExpression(High.BinaryExpressionNode binary)
    {
        var leftType = _typeMap.GetType(binary.Left);
        var rightType = _typeMap.GetType(binary.Left);

        var coercedLeft = binary.Left;
        var coercedRight = binary.Right;

        if (leftType.CanonicalName() != rightType.CanonicalName())
        {
            var targetType = _typeMap.GetType(binary);

            coercedLeft = Coerce(binary.Left, leftType, targetType);
            coercedRight = Coerce(binary.Right, rightType, targetType);
        }
        
        return new Low.BinaryExpressionNode(Expression(coercedLeft), binary.Operator, Expression(coercedRight));
    }
    
    private High.ExpressionNode Coerce(High.ExpressionNode expression, High.TypeNode sourceType, High.TypeNode targetType)
    {
        if (expression is High.IntegerLiteralNode intLit && targetType is High.FloatTypeNode)
        {
            return new High.FloatLiteralNode(intLit.Value, expression.Span);
        }
        if (expression is High.FloatLiteralNode floatLit && targetType is High.IntegerTypeNode)
        {
            return new High.IntegerLiteralNode((long)floatLit.Value, expression.Span);
        }
        if (sourceType is High.IntegerTypeNode intType && targetType is High.FloatTypeNode)
        {
            return new High.CastIntToFloatNode(expression, targetType, intType.Signed, expression.Span);
        }
        if (sourceType is High.SignedIntTypeNode or High.UnsignedIntTypeNode && targetType is High.SignedIntTypeNode or High.UnsignedIntTypeNode)
        {
            return new High.ExtendIntegerNode(expression, targetType, expression.Span);
        }

        if (sourceType is High.FloatTypeNode && targetType is High.FloatTypeNode)
        {
            return new High.ExtendFloatNode(expression, targetType, expression.Span);
        }

        return expression;
    }

    private Low.StructFieldInitializationExpressionNode StructFieldInitializationExpression(High.StructFieldInitializationExpressionNode structFieldInitialization)
    {
        return new Low.StructFieldInitializationExpressionNode(
            structFieldInitialization.NominalType.Name,
            structFieldInitialization.FieldInitializers.Select(
                x => new Low.StructFieldInitializerNode(x.FieldName, Expression(x.Value))).ToList()
            );
    }
    
    private Low.VariableDeclarationNode Variable(High.VariableDeclarationNode variable)
    {
        var explicitType = variable.TypeAnnotation != null ? Type(variable.TypeAnnotation) : null;
        var initializer = Expression(variable.Initializer);
        
        return new Low.VariableDeclarationNode(variable.IsMutable, variable.Name, explicitType, initializer);
    }

    private Low.IfStatementNode IfElse(High.IfElseStatement ifElse)
    {var condition = Expression(ifElse.Condition);
        var thenBranch = BlockStatement(ifElse.ThenBranch);

        if (ifElse.ElseBranch != null)
        {
            var elseBranch = BlockStatement(ifElse.ElseBranch);
            return new Low.IfElseStatementNode(condition, thenBranch, elseBranch);
        }

        return new Low.IfStatementNode(condition, thenBranch);
    }
    
    private Low.CallExpressionNode CallExpression(High.CallExpressionNode call)
    {
        var callee = Expression(call.Callee);
        var args = call.Arguments.Select(Expression).ToList();
        return new Low.CallExpressionNode(callee, args);
    }

    private Low.BlockStatementNode BlockStatement(High.BlockStatementNode blockStatement)
    {
        var statements = blockStatement.Statements.Select(Statement).ToList();
        return new Low.BlockStatementNode(statements);
    }
}