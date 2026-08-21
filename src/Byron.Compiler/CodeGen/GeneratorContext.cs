using System.Text;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.CodeGen;

public class GeneratorContext
{
    private readonly StringBuilder _irOutputBuilder = new();
    private readonly Dictionary<string, SymbolAddress> _symbolTable = new();
    private readonly Dictionary<string, StructLayout> _structLayouts = new();
    private readonly Dictionary<string, LlvmType> _functionSignatures = new();
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
    
    public string GetGeneratedIr() => _irOutputBuilder.ToString();
    
    public SymbolAddress LookupVariable(string name) => _symbolTable.TryGetValue(name, out var address)
        ? address
        : throw new KeyNotFoundException($"Compiler error: Undefined variable '{name}' requested.");
    
    public void DeclareVariable(string name, SymbolAddress register) => _symbolTable[name] = register;
    
    public void RegisterFunction(string functionName, LlvmType returnType)
    {
        _functionSignatures[functionName] = returnType;
    }
    
    public void RegisterStructLayout(StructLayout layout)
    {
        _structLayouts[layout.Name] = layout;
    }
    
    public StructLayout GetStructLayout(string structName) => _structLayouts.TryGetValue(structName, out var layout)
        ? layout
        : throw new ByronCodeGenerationException($"Compiler error: Unknown struct layout '{structName}'.");
    
    public LlvmType GetFunctionReturnType(string functionName) 
        => _functionSignatures.TryGetValue(functionName, out var returnType)
            ? returnType
            : throw new ByronCodeGenerationException($"Compiler error: Unknown function '{functionName}'.");

    public void PushLoop(string continueLabel, string breakLabel) 
        => _loopStack.Push((continueLabel, breakLabel));

    public void PopLoop() 
        => _loopStack.Pop();

    public (string ContinueLabel, string BreakLabel) CurrentLoop 
        => _loopStack.Count > 0 
            ? _loopStack.Peek() 
            : throw new ByronCodeGenerationException("Break/Continue used outside of loop");
}