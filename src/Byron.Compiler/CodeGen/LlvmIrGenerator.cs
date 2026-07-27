using Byron.Compiler.AST.LowLevel;
using Byron.Compiler.Exceptions;

namespace Byron.Compiler.CodeGen;

public partial class LlvmIrGenerator
{
    private readonly GeneratorContext _context = new();

    public string Generate(ProgramNode program)
    {
        foreach (var declaration in program.Declarations)
        {
            GenerateTopLevelDeclaration(declaration);
        }
        return _context.GetGeneratedIr();
    }

    private void GenerateTopLevelDeclaration(TopLevelDeclarationNode node)
    {
        switch (node)
        {
            case FunctionDeclarationNode func:
                GenerateFunctionDeclaration(func);
                break;
            default:
                throw new ByronNotImplementedException(node.GetType().Name, this);
        }
    }

    private void GenerateFunctionDeclaration(FunctionDeclarationNode node)
    {
        _context.ResetRegisters();
        
        var returnType = MapType(node.ReturnType);
        
        var functionParameterIr = string.Join(", ", node.Parameters.Select((parameterNode, i) => $"{MapType(parameterNode.Type)} %arg_{i}"));

        _context.EmitLine($"define {returnType} @{node.Name}({functionParameterIr}) {{");

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
            var param = node.Parameters[i];
            var stackPointer = $"%{param.Name}.addr";
            var typeStr = MapType(param.Type);
        
            _context.EmitLine($"    {stackPointer} = alloca {typeStr}");
        
            _context.EmitLine($"    store {typeStr} %arg_{i}, {typeStr}* {stackPointer}");
        
            _context.DeclareVariable(param.Name, stackPointer);
        }
    }

    private static bool IsUnsignedLlvmType(string llvmType) => llvmType.StartsWith('u');

    private string MapType(TypeNode node)
    {
        return node switch
        {
            VoidTypeNode => "void",
            UnitTypeNode => "void",
            
            Int8TypeNode => "i8",
            Int16TypeNode => "i16",
            Int32TypeNode => "i32",
            Int64TypeNode => "i64",
        
            UInt8TypeNode => "i8",
            UInt16TypeNode => "i16",
            UInt32TypeNode => "i32",
            UInt64TypeNode => "i64",
        
            Float32TypeNode => "float",
            Float64TypeNode => "double",
        
            BoolTypeNode => "i1",
            RuneTypeNode => "i32",
        
            ReferenceTypeNode r => $"{MapType(r.Target)}*",
            _ => throw new ByronNotImplementedException($"Type mapping for {node.GetType().Name}", this)
        };
    }
}