using System.Diagnostics.CodeAnalysis;

namespace Byron.Compiler.AST;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class PrimitiveTypeNames
{
    public const string @void = "void";
    public const string i8 = "i8";
    public const string i16 = "i16";  
    public const string i32 = "i32";
    public const string i64 = "i64";
    public const string u8 = "u8";
    public const string u16 = "u16";  
    public const string u32 = "u32";
    public const string u64 = "u64";
    public const string f32 = "f32";
    public const string f64 = "f64";
    public const string boolean = "boolean";
    public const string rune = "rune";
}
