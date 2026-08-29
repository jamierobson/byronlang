using ReceiverBindingOwnership = Byron.Compiler.AST.ReceiverBindingOwnership;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;

public class TraitImplementationValidator(GlobalSymbolTableLookup globalSymbolTableLookup, Diagnostics diagnostics)
{
    public void Validate(ModuleDeclarationNode module, ImplementBlockDeclarationNode block)
    {
        if (block.TraitNode is null)
        {
            return;
        }

        if (!globalSymbolTableLookup.TryGetTrait(module, block.TraitNode.Symbol, out var trait))
        {
            diagnostics.UndeclaredTrait(block.TraitNode);
            return;
        }

        if (!globalSymbolTableLookup.TryGetStruct(block.TypeNode, out var @struct))
        {
            diagnostics.UndeclaredType(block.TypeNode, "implement block type");
            return;
        };

        foreach (var requiredField in trait.RequiredFields)
        {
            if (!@struct.Fields.Any(x => x.Name == requiredField.Name && x.Type.Symbol == requiredField.Type.Symbol))
            {
                diagnostics.MissingTraitImplementationField(block, requiredField.Name);
            }
        }

        foreach (var requiredFunction in trait.RequiredFunctions)
        {
            var implementingDeclaration = block.FunctionDeclarations.SingleOrDefault(x => x.Signature.Name == requiredFunction.Name);
            if (implementingDeclaration is null)
            {
                diagnostics.MissingTraitImplementationFunction(block, requiredFunction.Name);
            }
            else
            {             
                if (implementingDeclaration.Signature.Parameters.Count != requiredFunction.Parameters.Count)
                {
                    var requiredFunctionSignature = SignatureString(requiredFunction);
                    var declaredFunctionSignature = SignatureString(implementingDeclaration.Signature);
                    diagnostics.InvalidTraitImplementationFunctionSignature(block, implementingDeclaration.Signature.Name, requiredFunctionSignature, declaredFunctionSignature);
                    break;
                }

                if (implementingDeclaration.Signature.ReturnType.Symbol != requiredFunction.ReturnType.Symbol)
                {
                    var requiredFunctionSignature = SignatureString(requiredFunction);
                    var declaredFunctionSignature = SignatureString(implementingDeclaration.Signature);
                    diagnostics.InvalidTraitImplementationFunctionSignature(block, implementingDeclaration.Signature.Name, requiredFunctionSignature, declaredFunctionSignature);
                    continue;
                }

                for (var i = 0; i < requiredFunction.Parameters.Count; i++)
                {
                    if (i == 0 && implementingDeclaration.Signature.Parameters[i].Type is SelfTypeNode)
                    {
                        continue;
                    }
                    
                    if (implementingDeclaration.Signature.Parameters[i].Type.Symbol != requiredFunction.Parameters[i].Type.Symbol)
                    {
                        var requiredFunctionSignature = SignatureString(requiredFunction);
                        var declaredFunctionSignature = SignatureString(implementingDeclaration.Signature);
                        diagnostics.InvalidTraitImplementationFunctionSignature(block, implementingDeclaration.Signature.Name, requiredFunctionSignature, declaredFunctionSignature);
                        break;
                    }
                }
            }
        }
    } 

    private string SelfSignatureString(ReceiverBindingOwnership ownership)
    {
        return ownership switch
        {
            ReceiverBindingOwnership.Owned => "Owned<Self>",
            ReceiverBindingOwnership.ImmutableBorrow => "&Self",
            ReceiverBindingOwnership.MutableBorrow => "&var Self",
            _ => "Self"
        };
    }
    
    public string SignatureString(ParameterNode parameter) => parameter.Type is SelfTypeNode
        ? SelfSignatureString(parameter.Ownership)
        : parameter.Type.Symbol.ToString();
    
    public string SignatureString(FunctionSignatureNode functionSignature) =>
        $"{functionSignature.Name}({string.Join(',', functionSignature.Parameters.Select(SignatureString))}): {functionSignature.ReturnType.Symbol}";
}