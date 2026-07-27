using Byron.Compiler.Exceptions;
using High = Byron.Compiler.AST.HighLevel;
using Low = Byron.Compiler.AST.LowLevel;

namespace Byron.Compiler.Parser;

public class ByronLoweringPass(High.ProgramNode highLevelAst)
{
    public Low.ProgramNode Lower()
    {
        var declarations = highLevelAst.Declarations
        .Select(TopLevelDeclaration)
        .ToList();

        return new Low.ProgramNode(declarations);
    }

    private Low.TopLevelDeclarationNode TopLevelDeclaration(High.TopLevelDeclarationNode declaration)
    {
        return declaration switch
        {
            High.FunctionDeclarationNode function => FunctionDeclaration(function),
            _ => throw new ByronNotImplementedException(declaration.GetType(), this) 
        };
    }

    private Low.FunctionDeclarationNode FunctionDeclaration(High.FunctionDeclarationNode function)
    {
        var parameters = function.Parameters.Select(Parameter).ToList();
        var returnType = Type(function.ReturnType);
        var body = BlockStatement(function.Body);

        return new Low.FunctionDeclarationNode(function.Name, parameters, returnType, body);
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
            High.ReferenceTypeNode refType => new Low.ReferenceTypeNode(Type(refType.Target), refType.IsMutable),
            
            High.VoidTypeNode => new Low.VoidTypeNode(),
            High.UnitTypeNode => new Low.UnitTypeNode(),
            High.Int8TypeNode => new Low.Int8TypeNode(),
            High.Int16TypeNode => new Low.Int16TypeNode(),
            High.Int32TypeNode => new Low.Int32TypeNode(),
            High.Int64TypeNode => new Low.Int64TypeNode(),
            High.UInt8TypeNode => new Low.UInt8TypeNode(),
            High.UInt16TypeNode => new Low.UInt16TypeNode(),
            High.UInt32TypeNode => new Low.UInt32TypeNode(),
            High.UInt64TypeNode => new Low.UInt64TypeNode(),
            High.Float32TypeNode => new Low.Float32TypeNode(),
            High.Float64TypeNode => new Low.Float64TypeNode(),
            High.BoolTypeNode => new Low.BoolTypeNode(),
            High.RuneTypeNode => new Low.RuneTypeNode(),

            _ => throw new ByronNotImplementedException(type.GetType(), this)
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
            _ => throw new ByronNotImplementedException(statement.GetType(), this)
        };
    }

    private Low.ExpressionNode Expression(High.ExpressionNode expression)
    {
        return expression switch
        {
            High.IntegerLiteralNode intLiteral => new Low.IntegerLiteralNode(intLiteral.Value),
            High.BoolLiteralNode boolLiteral => new Low.BoolLiteralNode(boolLiteral.Value),
            High.VariableExpressionNode variable => new Low.VariableExpressionNode(variable.Name),
            High.CallExpressionNode call => CallExpression(call),
            High.BinaryExpressionNode binary => new Low.BinaryExpressionNode(Expression(binary.Left), binary.Operator, Expression(binary.Right)),

            // Lowerable expressions here

            _ => throw new ByronNotImplementedException(expression.GetType(), this)
        };
    }
    
    private Low.VariableDeclarationNode Variable(High.VariableDeclarationNode variable)
    {
        var explicitType = variable.ExplicitType != null ? Type(variable.ExplicitType) : null;
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