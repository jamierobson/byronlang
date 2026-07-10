using System.Text;

namespace Byron.Compiler.CodeGen;

public class GeneratorContext
{
    private readonly StringBuilder _irOutputBuilder = new();
    private int _nextRegister = 1;

    public void EmitLine(string line) => _irOutputBuilder.AppendLine(line);
    public void Emit(string text) => _irOutputBuilder.Append(text);

    public string AllocateRegister() => $"%{_nextRegister++}";

    public void ResetRegisters() => _nextRegister = 1;

    public string GetGeneratedIr() => _irOutputBuilder.ToString();
}