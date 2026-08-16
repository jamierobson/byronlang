namespace Byron.Compiler.AST.HighLevel;

public record PrimitiveTypeSymbol(CanonicalName CanonicalName, int ByteSize, bool IsSigned);

public record ParameterSymbol(string Name, TypeNode Type, ReceiverBindingOwnership OwnershipBinding)
{
    public const string SelfArgumentName = "self";   
}

public record FunctionSymbol(
    CanonicalName CanonicalName,
    string[] ModulePath,
    string Name,
    List<ParameterSymbol> Parameters,
    TypeNode ReturnType,
    FunctionDeclarationNode Declaration
)
{
    public bool SupportsMethodInvocation() => Parameters.Count > 0 && Parameters[0].Name == ParameterSymbol.SelfArgumentName;

    public ParameterSymbol Self()
    {
        if (!SupportsMethodInvocation())
        {
            throw new InvalidOperationException($"Cannot get {ParameterSymbol.SelfArgumentName} argument for a function who doesn't support method invocation.");
        }
        return Parameters[0];
    }
}
