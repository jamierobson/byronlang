namespace Byron.Compiler.AST.LowLevel;

public record PrimitiveTypeSymbol(string CanonicalName, int ByteSize, bool IsSigned);

public record ParameterSymbol(string Name, TypeNode Type);

public record FunctionSymbol(
    string CanonicalName,
    string[] ModulePath,
    string Name,
    List<ParameterSymbol> Parameters,
    TypeNode ReturnType,
    FunctionDeclarationNode Declaration
);
