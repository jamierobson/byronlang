using Byron.Compiler.AST.LowLevel;

namespace Byron.Compiler.CodeGen;

public partial class LlvmIrGenerator
{
    private readonly GeneratorContext _context = new();

    public string Generate(ProgramNode program)
    {
        foreach (var structDeclaration in program.Declarations.OfType<StructDeclarationNode>())
        {
            RegisterStructLayout(structDeclaration);
        }

        foreach (var functionDeclaration in program.Declarations.OfType<FunctionDeclarationNode>())
        {
            _context.RegisterFunction(functionDeclaration.Name, LlvmType.From(functionDeclaration.ReturnType));
        }

        foreach (var functionDeclaration in program.Declarations.OfType<FunctionDeclarationNode>())
        {
            GenerateFunctionDeclaration(functionDeclaration);
        }
        
        return _context.GetGeneratedIr();
    }
    
    private void RegisterStructLayout(StructDeclarationNode structDeclaration)
    {
        var fields = structDeclaration.Fields.Select(x => (x.Name, x.Type)).ToList();
        var layout = StructLayout.CalculateLayout(structDeclaration.Name, fields);
        _context.RegisterStructLayout(layout);
        var llvmFieldTypes = string.Join(", ", structDeclaration.Fields.Select(f => LlvmType.From(f.Type)));
        
        _context.EmitLine($"%{structDeclaration.Name} = type {{ {llvmFieldTypes} }}");
        _context.EmitLine(string.Empty);
    }

    private void GenerateFunctionDeclaration(FunctionDeclarationNode node)
    {
        _context.ResetRegisters();
        
        var returnType = LlvmType.From(node.ReturnType);
        _context.RegisterFunction(node.Name, returnType);
        
        var functionParameterIr = string.Join(", ", node.Parameters.Select((parameterNode, i) => $"{LlvmType.From(parameterNode.Type)} %arg_{i}"));

        _context.EmitLine($"define {LlvmType.From(node.ReturnType)} @{node.Name}({functionParameterIr}) {{");

        MoveArgumentsOnToStackFrame(node);
        
        GenerateBlockStatement(node.Body);
        
        // Add a return when reaching the end of a void function
        if (node.ReturnType is VoidTypeNode or UnitTypeNode)
        {
            _context.EmitLine("    ret void");
        }
        
        _context.EmitLine("}\n");
    }

    private void MoveArgumentsOnToStackFrame(FunctionDeclarationNode node)
    {
        for (var i = 0; i < node.Parameters.Count; i++)
        {
            var parameter = node.Parameters[i];
            var stackPointerName = $"{parameter.Name}.addr";
            var stackPointerRegister = $"%{stackPointerName}";
            
            var llvmType = LlvmType.From(parameter.Type);
            var symbolAddress = new SymbolAddress(new Value.Register(stackPointerName), llvmType);
            
            _context.EmitLine($"    {stackPointerRegister} = alloca {llvmType}");
        
            _context.EmitLine($"    store {llvmType} %arg_{i}, {llvmType}* {stackPointerRegister}");
        
            _context.DeclareVariable(parameter.Name, symbolAddress);
        }
    }
}