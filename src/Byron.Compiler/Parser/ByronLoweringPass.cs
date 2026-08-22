using Byron.Compiler.AST;
using Byron.Compiler.Exceptions;
using Byron.Compiler.SemanticAnalysis;
using High = Byron.Compiler.AST.HighLevel;
using Low = Byron.Compiler.AST.LowLevel;

namespace Byron.Compiler.Parser;

public class ByronLoweringPass
{
    private readonly GlobalSymbolTable _globalSymbolTable;
    private readonly High.ProgramNode _ast;
    private readonly TypeMap _highLevelExpressionTypeMap;
    private readonly Dictionary<High.TypeNode, Low.TypeNode> _highToLowLevelTypeMap = new();
    
    public ByronLoweringPass(SemanticAnalysisResult semanticAnalysisResult)
    {
        if (!semanticAnalysisResult.Success)
        {
            throw new ByronLowLevelParserException("Unable to lower an invalid AST");
        }
        
        (_ast, _globalSymbolTable, _highLevelExpressionTypeMap) = semanticAnalysisResult;
    }
    
    public LoweredProgram Lower()
    {
        var declarations = _ast.RootModules
        .SelectMany(Module)
        .ToList();

        return new LoweredProgram(new Low.ProgramNode(declarations), _highToLowLevelTypeMap, _highLevelExpressionTypeMap);
    }

    private List<Low.TopLevelDeclarationNode> Module(High.FileModuleNode fileModule)
    {
        var structs = fileModule.Declarations.Structs.Select(StructDeclaration).ToList();
        var functions = fileModule
            .Declarations.ImplementBlocks.SelectMany(x => x.FunctionDeclarations)
            .Union(fileModule.Declarations.Functions)
            .Select(FunctionDeclaration).ToList();
        
        return [..structs, ..functions];
    }

    private Low.StructDeclarationNode StructDeclaration(High.StructDeclarationNode @struct)
    {
        var fields = @struct.Fields.Select(x => new Low.StructFieldNode(x, x.Name, Type(x.Type))).ToList();
        if (_globalSymbolTable.NominalTypes.CanonicalNames.TryGetValue(@struct.Type, out var canonicalName))
        {
            return new Low.StructDeclarationNode(@struct, canonicalName.ToString().Mangle(), fields);
        }
        
        throw new ByronLowLevelParserException($"Struct {@struct.Symbol} is not defined");
    } 

    private Low.FunctionDeclarationNode FunctionDeclaration(High.FunctionDeclarationNode declaration)
    {
        var parameters = declaration.Signature.Parameters.Select(Parameter).ToList();
        var returnType = Type(declaration.Signature.ReturnType);
        var body = BlockStatement(declaration.Body);

        if (_globalSymbolTable.Functions.CanonicalNames.TryGetValue(declaration, out var canonicalName))
        {
            var signature = new Low.FunctionSignatureNode(declaration.Signature, canonicalName.ToString().Mangle(), parameters, returnType);
            return new Low.FunctionDeclarationNode(declaration, signature, body);
        }
        
        throw new ByronLowLevelParserException($"{declaration.Symbol} is not defined");
    }

    private Low.ParameterNode Parameter(High.ParameterNode parameter)
    {
        var type = Type(parameter.Type);

        if (parameter.Ownership is ReceiverBindingOwnership.MutableBorrow or ReceiverBindingOwnership.ImmutableBorrow && type is not Low.ReferenceTypeNode)
        {
            type = new Low.ReferenceTypeNode(parameter.Type, type);
        }
        
        return new Low.ParameterNode(parameter, parameter.Name, parameter.Ownership, type);
    }

    private Low.TypeNode Type(High.TypeNode type)
    {
        Low.TypeNode loweredType = type switch
        {
            High.ReferenceTypeNode referenceType => new Low.ReferenceTypeNode(referenceType, Type(referenceType.Target)),
            High.NominalTypeNode userDeclaredType => NominalTypeNode(userDeclaredType),
            High.SelfTypeNode self => Type(self.ScopedType),
            High.VoidTypeNode @void => new Low.VoidTypeNode(@void),
            High.SignedIntTypeNode signed => new Low.SignedIntTypeNode(signed, signed.BitWidth, signed.Signed),
            High.UnsignedIntTypeNode unsigned => new Low.UnsignedIntTypeNode(unsigned, unsigned.BitWidth, unsigned.Signed),
            High.FloatTypeNode @float => new Low.FloatTypeNode(@float, @float.BitWidth),
            High.BoolTypeNode @bool => new Low.BoolTypeNode(@bool),
            High.RuneTypeNode rune => new Low.RuneTypeNode(rune),

            _ => throw new ByronNotImplementedException(type.GetType(), this, type.Span)
        };

        _highToLowLevelTypeMap[type] = loweredType;

        return loweredType;
    }

    private Low.TypeNode NominalTypeNode(High.NominalTypeNode userDeclaredType)
    {
        var canonicalName = _globalSymbolTable.NominalTypes.CanonicalNames[userDeclaredType];
        return new Low.NominalTypeNode(userDeclaredType, canonicalName.ToString().Mangle());
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
            High.ExpressionStatementNode expressionStatement => new Low.ExpressionStatementNode(expressionStatement, Expression(expressionStatement.Expression)),
            _ => throw new ByronNotImplementedException(statement.GetType(), this, statement.Span)
        };
    }

    private Low.ExpressionNode Expression(High.ExpressionNode expression)
    {
        return expression switch
        {
            High.FloatLiteralNode floatLiteral => new Low.FloatLiteralNode(floatLiteral, floatLiteral.Value),
            High.IntegerLiteralNode intLiteral => new Low.IntegerLiteralNode(intLiteral, intLiteral.Value),
            High.BooleanLiteralNode boolLiteral => new Low.BoolLiteralNode(boolLiteral, boolLiteral.Value),
            High.VariableExpressionNode variable => VariableExpression(variable),
            High.MethodCallExpression call => MethodSyntaxCallExpression(call),
            High.FreeFunctionCallExpressionNode call => FreeFunctionCallExpression(call),
            High.BinaryExpressionNode binary => CoercedBinaryExpression(binary), 
            High.StructFieldInitializationExpressionNode structFieldInitialization => StructFieldInitializationExpression(structFieldInitialization),
            High.MemberAccessExpressionNode memberAccess => new Low.MemberAccessExpressionNode(memberAccess, Expression(memberAccess.Target), memberAccess.MemberName),
            High.DereferenceExpressionNode dereference => new Low.DereferenceExpressionNode(dereference, Expression(dereference.Target)),
            High.AddressOfExpressionNode address => new Low.AddressOfExpressionNode(address, Expression(address.Target)),
            
            // These default values should never be hit. However, the high cast expressions only work with TargetType as a TypeNode. If that ever happens, we will cry. 
            High.CastFloatToIntNode floatToInt => new Low.CastFloatToIntNode(floatToInt,Expression(floatToInt.Operand), Type(floatToInt.TargetType) as Low.IntegerTypeNode ?? throw new ByronCodeGenerationException("Invalid target type for generating CastFloatToIntNode")), 
            High.CastIntToFloatNode intToFloat => new Low.CastIntToFloatNode(intToFloat,Expression(intToFloat.Operand), Type(intToFloat.TargetType) as Low.FloatTypeNode ?? throw new ByronCodeGenerationException("Invalid target type for generating CastIntToFloatNode"), intToFloat.SourceTypeIsSigned),
            High.ExtendIntegerNode extendInt => new Low.ExtendIntegerNode(extendInt,Expression(extendInt.Operand), Type(extendInt.TargetType) as Low.IntegerTypeNode ?? throw new ByronCodeGenerationException("Invalid target type for generating ExtendIntegerNode")),
            High.ExtendFloatNode extendFloat => new Low.ExtendFloatNode(extendFloat,Expression(extendFloat.Operand), Type(extendFloat.TargetType) as Low.FloatTypeNode ?? throw new ByronCodeGenerationException("Invalid target type for generating ExtendFloatNode")),

            // Lowerable expressions here
            _ => throw new ByronNotImplementedException(expression.GetType(), this, expression.Span)
        };
    }
    
    private Low.VariableExpressionNode VariableExpression(High.VariableExpressionNode variable)
    {
        if (variable is High.FunctionInvocationVariableExpressionNode invocation)
        {
            return new Low.VariableExpressionNode(invocation, invocation.Function.Symbol.ToString().Mangle());
        }
        
        return new Low.VariableExpressionNode(variable, variable.Name);
    }

    private Low.BinaryExpressionNode CoercedBinaryExpression(High.BinaryExpressionNode binary)
    {
        var leftType = _highLevelExpressionTypeMap.GetType(binary.Left);
        var rightType = _highLevelExpressionTypeMap.GetType(binary.Right);

        var coercedLeft = binary.Left;
        var coercedRight = binary.Right;

        if (leftType.Symbol != rightType.Symbol)
        {
            var targetType = _highLevelExpressionTypeMap.GetType(binary);

            coercedLeft = Coerce(binary.Left, leftType, targetType);
            coercedRight = Coerce(binary.Right, rightType, targetType);
        }
        
        return new Low.BinaryExpressionNode(binary, Expression(coercedLeft), binary.Operator, Expression(coercedRight));
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
            (Low.NominalTypeNode)Type(structFieldInitialization.NominalType), //This cast is expected to always be true, since Type maps high to low nominal type  
            structFieldInitialization.FieldInitializers.Select(
                x => new Low.StructFieldInitializerNode(x, x.FieldName, Expression(x.Value))).ToList()
            );
    }
    
    private Low.VariableDeclarationNode Variable(High.VariableDeclarationNode variable)
    {
        var explicitType = variable.TypeAnnotation != null ? Type(variable.TypeAnnotation) : null;
        var initializer = Expression(variable.Initializer);
        
        return new Low.VariableDeclarationNode(variable, variable.Name, variable.IsMutable, explicitType, initializer);
    }

    private Low.IfStatementNode IfElse(High.IfElseStatement ifElse)
    {
        var condition = Expression(ifElse.Condition);
        var thenBranch = BlockStatement(ifElse.ThenBranch);

        if (ifElse.ElseBranch != null)
        {
            var elseBranch = BlockStatement(ifElse.ElseBranch);
            return new Low.IfElseStatementNode(ifElse, condition, thenBranch, elseBranch);
        }

        return new Low.IfStatementNode(ifElse, condition, thenBranch);
    }
    
    private Low.CallExpressionNode FreeFunctionCallExpression(High.FreeFunctionCallExpressionNode call)
    {
        var callee = Expression(call.Callee);
        var args = call.Arguments.Select(Expression).ToList();
        return new Low.CallExpressionNode(call, callee, args);
    }
    
    private Low.CallExpressionNode MethodSyntaxCallExpression(High.MethodCallExpression call)
    {
        var receiver = Expression(call.Receiver);
        var callee = Expression(call.Callee);
        List<Low.ExpressionNode> args = [receiver, ..call.Arguments.Select(Expression)];
        return new Low.CallExpressionNode(call, callee, args);
    }

    private Low.BlockStatementNode BlockStatement(High.BlockStatementNode blockStatement)
    {
        var statements = blockStatement.Statements.Select(Statement).ToList();
        return new Low.BlockStatementNode(blockStatement, statements);
    }
}