using System.Text;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.CodeGen;

public class GeneratorContext
{
    private readonly StringBuilder _irOutputBuilder = new();
    private readonly Dictionary<string, string> _symbolTable = new();
    private readonly Stack<(string ContinueLabel, string BreakLabel)> _loopStack = new();
    
    private int _nextRegister = 1;
    private int _nextLabelId = 0;

    public void EmitLine(string line) => _irOutputBuilder.AppendLine(line);
    public void Emit(string text) => _irOutputBuilder.Append(text);

    public string AllocateRegister() => $"%{_nextRegister++}";
    public int AllocateLabelId() => _nextLabelId++;

    public void ResetRegisters()
    {
        _nextRegister = 1;
        _nextLabelId = 0;
        _symbolTable.Clear();
    }
    
    public string LookupVariable(string name) => _symbolTable.TryGetValue(name, out var register)
        ? register
        : throw new KeyNotFoundException($"Compiler error: Undefined variable '{name}' requested.");

    public string GetGeneratedIr() => _irOutputBuilder.ToString();
    
    public void DeclareVariable(string name, string register) => _symbolTable[name] = register;

    public void PushLoop(string continueLabel, string breakLabel) 
        => _loopStack.Push((continueLabel, breakLabel));

    public void PopLoop() 
        => _loopStack.Pop();

    public (string ContinueLabel, string BreakLabel) CurrentLoop 
        => _loopStack.Count > 0 
            ? _loopStack.Peek() 
            : throw new ByronCodeGenerationException("Break/Continue used outside of loop");
}