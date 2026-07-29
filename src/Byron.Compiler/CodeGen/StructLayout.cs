using Byron.Compiler.AST.LowLevel;

namespace Byron.Compiler.CodeGen;

public class StructLayout
{
    public string Name { get; }
    public Dictionary<string, int> FieldIndices { get; } = new();
    public Dictionary<string, TypeNode> FieldTypes { get; } = new();

    public StructLayout(string name, List<(string Name, TypeNode Type)> fields)
    {
        Name = name;
        for (int i = 0; i < fields.Count; i++)
        {
            FieldIndices[fields[i].Name] = i;
            FieldTypes[fields[i].Name] = fields[i].Type;
        }
    }

    public int GetFieldIndex(string fieldName) => FieldIndices[fieldName];
    public TypeNode GetFieldType(string fieldName) => FieldTypes[fieldName];

    public static StructLayout CalculateLayout(string name, List<(string Name, TypeNode Type)> fields)
    {
        return new StructLayout(name, fields);
    }
}

public class SymbolEnvironment(SymbolEnvironment? parentScope = null)
{
    private readonly Dictionary<string, SymbolAddress> _variables = new();
    private readonly Dictionary<string, StructLayout> _structLayouts = new();
    private readonly SymbolEnvironment? _parentScope;
    
    public void RegisterVariable(string name, SymbolAddress address)
    {
        _variables[name] = address;
    }
    
    public SymbolAddress GetVariable(string name)
    {
        if (_variables.TryGetValue(name, out var addr))
        {
            return addr;
        }

        if (_parentScope != null)
        {
            return _parentScope.GetVariable(name);
        }

        throw new InvalidOperationException($"Lowering error: Undefined variable '{name}'.");
    }
    
    public void RegisterStructLayout(StructLayout layout)
    {
        _structLayouts[layout.Name] = layout;
    }

    public StructLayout GetStructLayout(string structName)
    {
        if (_structLayouts.TryGetValue(structName, out var layout))
        {
            return layout;
        }

        if (_parentScope != null)
        {
            return _parentScope.GetStructLayout(structName);
        }

        throw new InvalidOperationException($"Lowering error: Unknown struct type '{structName}'.");
    }
}