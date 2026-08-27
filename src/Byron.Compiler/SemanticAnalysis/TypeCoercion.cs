using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class TypeCoercion(GlobalSymbolTableLookup globalSymbolTableLookup, CanonicalResolvingTypeMap typeMap, Diagnostics diagnostics)
{
    public bool TryCoerce(ModuleDeclarationNode module, ExpressionNode expression, TypeNode targetType,
        [NotNullWhen(true)] out ExpressionNode? result)
    {
        if (expression is AddressOfExpressionNode addressOf)
        {
            result = addressOf;
            return true;
        }

        var sourceType = typeMap.GetType(expression);
        if (targetType is PrimitiveTypeNode p && sourceType.Symbol == p.Symbol)
        {
            result = expression;
            return true;
        }
        
        if (targetType is ReferenceTypeNode targetRef && sourceType.Symbol == targetRef.Target.Symbol)
        {
            result = new AddressOfExpressionNode(expression, targetRef.IsMutable, expression.Span);
            typeMap.SetType(module, result, targetType);
            return true;
        }

        if (sourceType is ReferenceTypeNode sourceRef && sourceRef.Target.Symbol == targetType.Symbol)
        {
            result = new DereferenceExpressionNode(expression, expression.Span);
            typeMap.SetType(module, result, targetType);
            return true;
        }
        
        if (globalSymbolTableLookup.TryResolveCanonicalType(module, targetType, out var canonicalType))
        {
            if (canonicalType.Symbol == sourceType.Symbol)
            {
                typeMap.SetType(module, expression, canonicalType);
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
                typeMap.SetType(intLiteral, targetType);
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
                typeMap.SetType(floatLiteral, targetType);
                return true;
            }

            result = null;
            return false;
        }

        if (expression is StructFieldInitializationExpressionNode initialization)
        {
            if (sourceType is not NominalTypeNode nominalType)
            {
                diagnostics.InvalidStructInitializationType(initialization.NominalType, initialization.Span);
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
        typeMap.SetType(expression, targetType);
    }

    private void SetType(CastExpressionNode expression, TypeNode targetType)
    {
        typeMap.SetType(expression, targetType);
    }
    

    public ExpressionNode AddCastsWhenRequired(ModuleDeclarationNode module, ExpressionNode expression, TypeNode targetType)
    {
        var sourceType = typeMap.GetType(expression);

        if (sourceType.Symbol == targetType.Symbol)
        {
            return expression;
        }

        var cast = Cast(expression, sourceType, targetType);
        typeMap.SetType(module, cast, targetType);
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
    
    public bool TryGetPreferredCoercionType(ExpressionNode leftExpression, TypeNode leftType,
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
}

