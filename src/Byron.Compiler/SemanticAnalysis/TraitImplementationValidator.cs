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

        if (!globalSymbolTableLookup.TryGetTrait(module, block.TraitNode.Symbol, out var resolvedTrait))
        {
            diagnostics.UndeclaredTrait(block.TraitNode);
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


                var traitModule = globalSymbolTableLookup.GetEncapsulatingModule(resolvedTrait);
                var canResolveImplementingReturnType = globalSymbolTableLookup.TryResolveCanonicalType(module, implementingDeclaration.Signature.ReturnType, out var implementingReturnType);
                var canResolveRequiredReturnType = globalSymbolTableLookup.TryResolveCanonicalType(traitModule, requiredFunction.ReturnType, out var requiredReturnType);

                if (!(canResolveImplementingReturnType && canResolveRequiredReturnType && implementingReturnType!.Symbol == requiredReturnType!.Symbol))
                {
                    var requiredFunctionSignature = SignatureString(requiredFunction);
                    var declaredFunctionSignature = SignatureString(implementingDeclaration.Signature);
                    diagnostics.InvalidTraitImplementationFunctionSignature(block, implementingDeclaration.Signature.Name, requiredFunctionSignature, declaredFunctionSignature);
                    continue;
                }

                for (var i = 0; i < requiredFunction.Parameters.Count; i++)
                {
                    if (i == 0 && requiredFunction.Parameters[i].Type is SelfTypeNode)
                    {
                        continue;
                    }
                    
                    var canResolveImplementingParameterType = globalSymbolTableLookup.TryResolveCanonicalType(module, implementingDeclaration.Signature.Parameters[i].Type, out var implementingParameterType);
                    var canResolveRequiredParameterType = globalSymbolTableLookup.TryResolveCanonicalType(traitModule, requiredFunction.Parameters[i].Type, out var requiredParameterType);
                    
                    if (!(canResolveImplementingParameterType && canResolveRequiredParameterType && implementingParameterType!.Symbol == requiredParameterType!.Symbol))
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