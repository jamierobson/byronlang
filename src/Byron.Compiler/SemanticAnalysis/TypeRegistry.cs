using System.Diagnostics.CodeAnalysis;
using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;

namespace Byron.Compiler.SemanticAnalysis;


public class PrimitiveTypeRegistry
{    
    public readonly IReadOnlyDictionary<string, PrimitiveTypeSymbol> Primitives = new Dictionary<string, PrimitiveTypeSymbol>
    {
        { PrimitiveTypeNames.i8, new PrimitiveTypeSymbol(PrimitiveTypeNames.i8, 1, true) },
        { PrimitiveTypeNames.i16, new PrimitiveTypeSymbol(PrimitiveTypeNames.i16, 2, true) },
        { PrimitiveTypeNames.i32, new PrimitiveTypeSymbol(PrimitiveTypeNames.i32, 4, true) },
        { PrimitiveTypeNames.i64, new PrimitiveTypeSymbol(PrimitiveTypeNames.i64, 8, true) },

        { PrimitiveTypeNames.u8, new PrimitiveTypeSymbol(PrimitiveTypeNames.u8, 1, false) },
        { PrimitiveTypeNames.u16, new PrimitiveTypeSymbol(PrimitiveTypeNames.u16, 2, false) },
        { PrimitiveTypeNames.u32, new PrimitiveTypeSymbol(PrimitiveTypeNames.u32, 4, false) },
        { PrimitiveTypeNames.u64, new PrimitiveTypeSymbol(PrimitiveTypeNames.u64, 8, false) },

        { PrimitiveTypeNames.f32, new PrimitiveTypeSymbol(PrimitiveTypeNames.f32, 4, true) },
        { PrimitiveTypeNames.f64, new PrimitiveTypeSymbol(PrimitiveTypeNames.f64, 8, true) },

        { PrimitiveTypeNames.boolean, new PrimitiveTypeSymbol(PrimitiveTypeNames.boolean, 1, false) },
        { PrimitiveTypeNames.rune, new PrimitiveTypeSymbol(PrimitiveTypeNames.rune, 4, false) },
        { PrimitiveTypeNames.@void, new PrimitiveTypeSymbol(PrimitiveTypeNames.@void, 1, false) },
    };
    
    public bool TryGet(string canonicalName, out int byteSize, out bool isSigned)
    {
        if (Primitives.TryGetValue(canonicalName, out var p))
        {
            byteSize = p.ByteSize;
            isSigned = p.IsSigned;
            return true;
        }

        byteSize = 0;
        isSigned = false;
        return false;
    }
    
    public bool ContainsKey(string canonicalName) => Primitives.ContainsKey(canonicalName);
}

public class StructRegistry
{
    private readonly Dictionary<string, StructDeclarationNode> _declarations = [];
    public bool ContainsKey(string canonicalName) => _declarations.ContainsKey(canonicalName);
    
    public bool TryRegister(StructDeclarationNode structDeclarationNode) => _declarations.TryAdd(structDeclarationNode.CanonicalName(), structDeclarationNode);
    
    public bool TryGet(string canonicalName, [NotNullWhen(true)]out StructDeclarationNode? @struct) => _declarations.TryGetValue(canonicalName, out @struct);

    public bool TryGetInScope(List<string> modulePath, string shortName, [NotNullWhen(true)] out StructDeclarationNode? @struct) => _declarations.TryGetValue(CanonicalNames.InModule(modulePath, shortName), out @struct) || _declarations.TryGetValue(shortName, out @struct);
    public bool TryGetFieldType(string canonicalName, string fieldName, [NotNullWhen(true)] out TypeNode? fieldType)
    {
        fieldType = null;
        if (_declarations.TryGetValue(canonicalName, out var structDeclaration))
        {
            foreach (var field in structDeclaration.Fields)
            {
                if (field.Name == fieldName)
                {
                    fieldType = field.Type;
                    return true;
                }
            }
        }
        
        return false;
    }
}

public class TypeRegistry
{
    private readonly PrimitiveTypeRegistry _primitiveRegistry = new();
    private readonly StructRegistry _structRegistry = new();

    public bool IsValidStructName(string canonicalName) => !_primitiveRegistry.ContainsKey(canonicalName);
    public bool TryRegister(StructDeclarationNode structDeclarationNode) => _structRegistry.TryRegister(structDeclarationNode);
    public bool TryGetStruct(string canonicalName, [NotNullWhen(true)]out StructDeclarationNode? @struct) => _structRegistry.TryGet(canonicalName, out @struct);
    public bool TryGetStructInScope(List<string> modulePath, string shortName, [NotNullWhen(true)] out StructDeclarationNode? @struct) => _structRegistry.TryGetInScope(modulePath, shortName, out @struct);
    public bool TryGetFieldType(string canonicalName, string fieldName, [NotNullWhen(true)] out TypeNode? fieldType) => _structRegistry.TryGetFieldType(canonicalName, fieldName, out fieldType);
 
    public bool IsValidType(TypeNode typeNode)
    {
        return typeNode switch
        {
            PrimitiveTypeNode => true,
            ReferenceTypeNode reference => IsValidType(reference.Target),
            NominalTypeNode nominal => _structRegistry.ContainsKey(nominal.CanonicalName()),
            _ => false
        };
    }
    
    public bool IsPrimitiveType(string canonicalName) => _primitiveRegistry.ContainsKey(canonicalName);
    
    public bool IsValidType(string canonicalName) =>  _structRegistry.ContainsKey(canonicalName) || _primitiveRegistry.ContainsKey(canonicalName);
    
    public bool IsValidTypeInScope(List<string> modulePath, string shortName)
    {
        return modulePath.Count == 0 && _structRegistry.ContainsKey(shortName)
            || _structRegistry.ContainsKey(CanonicalNames.InModule(modulePath, shortName))
            || _structRegistry.ContainsKey(shortName);
    }
}