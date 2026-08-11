using Byron.Compiler.AST;
using Byron.Compiler.Exceptions;
using Byron.Compiler.SemanticAnalysis;
using High = Byron.Compiler.AST.HighLevel;
using Low = Byron.Compiler.AST.LowLevel;

namespace Byron.Compiler.Parser;

public class ByronLoweringPass
{
    private readonly High.ProgramNode _ast;
    // private readonly TypeRegistry _typeRegistry;
    private readonly TypeMap _highLevelExpressionTypeMap;
    // private readonly FunctionRegistry _functionRegistry;
    private readonly Dictionary<High.TypeNode, Low.TypeNode> _highToLowLevelTypeMap = new();
    
    public ByronLoweringPass(SemanticAnalysisResult semanticAnalysisResult)
    {
        if (!semanticAnalysisResult.Success)
        {
            throw new ByronLowLevelParserException("Unable to lower an invalid AST");
        }
        
        (_ast, _, _highLevelExpressionTypeMap, _) = semanticAnalysisResult;
        // (_ast, _typeRegistry, _highLevelExpressionTypeMap, _functionRegistry) = semanticAnalysisResult;
    }
    
    public LoweredProgram Lower()
    {
        var declarations = _ast.Declarations
        .Select(TopLevelDeclaration)
        .ToList();

        return new LoweredProgram(new Low.ProgramNode(declarations), _highToLowLevelTypeMap, _highLevelExpressionTypeMap);
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
        var fields = @struct.Fields.Select(x => new Low.StructFieldNode(x, Type(x.Type))).ToList();
        return new Low.StructDeclarationNode(@struct, fields);
    } 

    private Low.FunctionDeclarationNode FunctionDeclaration(High.FunctionDeclarationNode declaration)
    {
        var parameters = declaration.Parameters.Select(Parameter).ToList();
        var returnType = Type(declaration.ReturnType);
        var body = BlockStatement(declaration.Body);

        return new Low.FunctionDeclarationNode(declaration, parameters, returnType, body);
    }

    private Low.ParameterNode Parameter(High.ParameterNode parameter)
    {
        var type = Type(parameter.Type);

        if (parameter.Ownership is ReceiverBindingOwnership.MutableBorrow or ReceiverBindingOwnership.ImmutableBorrow && type is not Low.ReferenceTypeNode)
        {
            type = new Low.ReferenceTypeNode(parameter.Type, type);
        }
        
        return new Low.ParameterNode(parameter, type);
    }

    private Low.TypeNode Type(High.TypeNode type)
    {
        Low.TypeNode loweredType = type switch
        {
            High.ReferenceTypeNode referenceType => new Low.ReferenceTypeNode(referenceType, Type(referenceType.Target)),
            High.NominalTypeNode userDeclaredType => new Low.NominalTypeNode(userDeclaredType),
            High.VoidTypeNode @void => new Low.VoidTypeNode(@void),
            High.SignedIntTypeNode signed => new Low.SignedIntTypeNode(signed),
            High.UnsignedIntTypeNode unsigned => new Low.UnsignedIntTypeNode(unsigned),
            High.FloatTypeNode @float => new Low.FloatTypeNode(@float),
            High.BoolTypeNode @bool => new Low.BoolTypeNode(@bool),
            High.RuneTypeNode rune => new Low.RuneTypeNode(rune),

            _ => throw new ByronNotImplementedException(type.GetType(), this, type.Span)
        };

        _highToLowLevelTypeMap[type] = loweredType;

        return loweredType;
    }

    private Low.StatementNode Statement(High.StatementNode statement)
    {
        return statement switch
        {
            High.BlockStatementNode block => BlockStatement(block),
            High.ReturnStatementNode @return => new Low.ReturnStatementNode(@return, @return.Expression != null ? Expression(@return.Expression) : null),
            High.YieldStatementNode yield => new Low.YieldStatementNode(yield, Expression(yield.Expression)),
            High.DiscardStatementNode discard => new Low.DiscardStatementNode(discard, Expression(discard.Initializer)),
            High.VariableDeclarationNode variable => Variable(variable),
            High.IfElseStatement ifElse => IfElse(ifElse),
            High.BreakStatement @break => new Low.BreakStatement(@break),
            High.ContinueStatement @continue => new Low.ContinueStatement(@continue),
            High.WhileStatement @while => new Low.WhileStatement(@while, Expression(@while.ContinuationCondition), BlockStatement(@while.Body)),
            High.AssignmentStatementNode assignment => new Low.AssignmentStatementNode(assignment, Expression(assignment.Target), Expression(assignment.Value)),
            _ => throw new ByronNotImplementedException(statement.GetType(), this, statement.Span)
        };
    }

    private Low.ExpressionNode Expression(High.ExpressionNode expression)
    {
        return expression switch
        {
            High.FloatLiteralNode floatLiteral => new Low.FloatLiteralNode(floatLiteral),
            High.IntegerLiteralNode intLiteral => new Low.IntegerLiteralNode(intLiteral),
            High.BoolLiteralNode boolLiteral => new Low.BoolLiteralNode(boolLiteral),
            High.VariableExpressionNode variable => new Low.VariableExpressionNode(variable),
            High.CallExpressionNode call => CallExpression(call),
            High.BinaryExpressionNode binary => CoercedBinaryExpression(binary), 
            High.StructFieldInitializationExpressionNode structFieldInitialization => StructFieldInitializationExpression(structFieldInitialization),
            High.MemberAccessExpressionNode memberAccess => new Low.MemberAccessExpressionNode(memberAccess, Expression(memberAccess.Target), memberAccess.MemberName),
            High.DereferenceExpressionNode dereference => new Low.DereferenceExpressionNode(dereference, Expression(dereference.Target)),
            
            // These default values should never be hit. However, the high cast expressions only work with TargetType as a TypeNode. If that ever happens, we will cry. 
            High.CastFloatToIntNode floatToInt => new Low.CastFloatToIntNode(floatToInt,Expression(floatToInt.Operand), Type(floatToInt.TargetType) as Low.IntegerTypeNode ?? throw new ByronCodeGenerationException("Invalid target type for generating CastFloatToIntNode")), 
            High.CastIntToFloatNode intToFloat => new Low.CastIntToFloatNode(intToFloat,Expression(intToFloat.Operand), Type(intToFloat.TargetType) as Low.FloatTypeNode ?? throw new ByronCodeGenerationException("Invalid target type for generating CastIntToFloatNode")),
            High.ExtendIntegerNode extendInt => new Low.ExtendIntegerNode(extendInt,Expression(extendInt.Operand), Type(extendInt.TargetType) as Low.IntegerTypeNode ?? throw new ByronCodeGenerationException("Invalid target type for generating ExtendIntegerNode")),
            High.ExtendFloatNode extendFloat => new Low.ExtendFloatNode(extendFloat,Expression(extendFloat.Operand), Type(extendFloat.TargetType) as Low.FloatTypeNode ?? throw new ByronCodeGenerationException("Invalid target type for generating ExtendFloatNode")),

            // Lowerable expressions here
            _ => throw new ByronNotImplementedException(expression.GetType(), this, expression.Span)
        };
    }

    private Low.BinaryExpressionNode CoercedBinaryExpression(High.BinaryExpressionNode binary)
    {
        var leftType = _highLevelExpressionTypeMap.GetType(binary.Left);
        var rightType = _highLevelExpressionTypeMap.GetType(binary.Right);

        var coercedLeft = binary.Left;
        var coercedRight = binary.Right;

        if (leftType.CanonicalName() != rightType.CanonicalName())
        {
            var targetType = _highLevelExpressionTypeMap.GetType(binary);

            coercedLeft = Coerce(binary.Left, leftType, targetType);
            coercedRight = Coerce(binary.Right, rightType, targetType);
        }
        
        return new Low.BinaryExpressionNode(binary, Expression(coercedLeft), Expression(coercedRight));
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
        if (sourceType is High.IntegerTypeNode intType && targetType is High.FloatTypeNode floatTarget)
        {
            return new High.CastIntToFloatNode(expression, floatTarget, intType.Signed, expression.Span);
        }
        if (sourceType is High.SignedIntTypeNode or High.UnsignedIntTypeNode && targetType is High.IntegerTypeNode intTarget)
        {
            return new High.ExtendIntegerNode(expression, intTarget, expression.Span);
        }

        if (sourceType is High.FloatTypeNode && targetType is High.FloatTypeNode targetFloat)
        {
            return new High.ExtendFloatNode(expression, targetFloat, expression.Span);
        }

        return expression;
    }

    private Low.StructFieldInitializationExpressionNode StructFieldInitializationExpression(High.StructFieldInitializationExpressionNode structFieldInitialization)
    {
        return new Low.StructFieldInitializationExpressionNode(
            structFieldInitialization,
            structFieldInitialization.FieldInitializers.Select(
                x => new Low.StructFieldInitializerNode(x, Expression(x.Value))).ToList()
            );
    }
    
    private Low.VariableDeclarationNode Variable(High.VariableDeclarationNode variable)
    {
        var explicitType = variable.TypeAnnotation != null ? Type(variable.TypeAnnotation) : null;
        var initializer = Expression(variable.Initializer);
        
        return new Low.VariableDeclarationNode(variable, explicitType, initializer);
    }

    private Low.IfStatementNode IfElse(High.IfElseStatement ifElse)
    {var condition = Expression(ifElse.Condition);
        var thenBranch = BlockStatement(ifElse.ThenBranch);

        if (ifElse.ElseBranch != null)
        {
            var elseBranch = BlockStatement(ifElse.ElseBranch);
            return new Low.IfElseStatementNode(ifElse, condition, thenBranch, elseBranch);
        }

        return new Low.IfStatementNode(ifElse, condition, thenBranch);
    }
    
    private Low.CallExpressionNode CallExpression(High.CallExpressionNode call)
    {
        var callee = Expression(call.Callee);
        var args = call.Arguments.Select(Expression).ToList();
        return new Low.CallExpressionNode(call, callee, args);
    }

    private Low.BlockStatementNode BlockStatement(High.BlockStatementNode blockStatement)
    {
        var statements = blockStatement.Statements.Select(Statement).ToList();
        return new Low.BlockStatementNode(blockStatement, statements);
    }
}