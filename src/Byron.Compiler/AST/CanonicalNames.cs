using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.AST;
public record CanonicalName(string[] ModulePath, string ShortName)
{
    public static string CanonicalModuleNameString(string[] modulePath) => string.Join('.', modulePath);
    public static string CanonicalNameString(string[] modulePath, string shortName) => modulePath.Length != 0 ? $"{CanonicalModuleNameString(modulePath)}.{shortName}" : shortName;
    protected virtual string ModulePathString => field ??= CanonicalModuleNameString(ModulePath);
    protected virtual string CanonicalNameAsString => field ??= CanonicalNameString(ModulePath, ShortName);
    public static CanonicalName From(string[] modulePath, string shortName) => new(modulePath, shortName); 
    public override string ToString() => CanonicalNameAsString;
    public string ToModulePathString() => ModulePathString;
}

public record ReferenceCanonicalName(TypeNode ReferencedType, bool IsMutable) : CanonicalName(ReferencedType.CanonicalName.ModulePath, ReferencedType.CanonicalName.ShortName)
{
    protected override string CanonicalNameAsString => field ??= $"&{(IsMutable ? "var " : "")}{ReferencedType}";
}