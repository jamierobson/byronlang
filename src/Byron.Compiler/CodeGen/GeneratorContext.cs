using System.Text;

namespace Byron.Compiler.CodeGen;

public class GeneratorContext
{
    private readonly StringBuilder _irOutputBuilder = new();
    private readonly Dictionary<string, string> _symbolTable = new();
    private int _nextRegister = 1;

    public void EmitLine(string line) => _irOutputBuilder.AppendLine(line);
    public void Emit(string text) => _irOutputBuilder.Append(text);

    public string AllocateRegister() => $"%{_nextRegister++}";

    public void ResetRegisters()
    {
        _nextRegister = 1;
        _symbolTable.Clear();
    }
    
    public string LookupVariable(string name) => _symbolTable.TryGetValue(name, out var register)
        ? register
        : throw new KeyNotFoundException($"Compiler error: Undefined variable '{name}' requested.");

    public string GetGeneratedIr() => _irOutputBuilder.ToString();
    
    public void DeclareVariable(string name, string register) => _symbolTable[name] = register;
}