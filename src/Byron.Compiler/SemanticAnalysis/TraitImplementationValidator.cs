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
                    diagnostics.InvalidTraitImplementationFunctionSignature(block, requiredFunction, implementingDeclaration.Signature);
                    break;
                }

                if (implementingDeclaration.Signature.ReturnType.Symbol != requiredFunction.ReturnType.Symbol)
                {
                    diagnostics.InvalidTraitImplementationFunctionSignature(block, requiredFunction, implementingDeclaration.Signature);
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
                        diagnostics.InvalidTraitImplementationFunctionSignature(block, requiredFunction, implementingDeclaration.Signature);
                        break;
                    }
                }
            }
        }
    }
}