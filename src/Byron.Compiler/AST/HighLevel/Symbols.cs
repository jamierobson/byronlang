namespace Byron.Compiler.AST.HighLevel;

public record PrimitiveTypeSymbol(CanonicalName CanonicalName, int ByteSize, bool IsSigned);
public record ParameterSymbol(string Name, TypeNode Type);

public record FunctionSymbol(
    CanonicalName CanonicalName,
    string[] ModulePath,
    string Name,
    List<ParameterSymbol> Parameters,
    TypeNode ReturnType,
    FunctionDeclarationNode Declaration
);
