using System.Text;
using LLVMSharp.Interop;

namespace Byron.Compiler.Driver;

public class InProcessExecutionEngine
{
    public static int Execute(string llvmIr)
    {
        LLVM.LinkInMCJIT();
        LLVM.InitializeNativeTarget();
        LLVM.InitializeNativeAsmPrinter();
        LLVM.InitializeNativeAsmParser();

        var context = LLVMContextRef.Create();
        LLVMModuleRef module;
        
        var utf8Bytes = Encoding.UTF8.GetBytes(llvmIr);
        var moduleName = "ByronMain"u8;
        
        unsafe
        {
            fixed (byte* irPin = utf8Bytes)
            fixed (byte* modulePin = moduleName)
            {
                var buffer = LLVM.CreateMemoryBufferWithMemoryRangeCopy((sbyte*)irPin, (UIntPtr)utf8Bytes.Length, (sbyte*)modulePin);
                if (!context.TryParseIR(buffer, out module, out var error))
                {
                    throw new Exception(error);
                }
            }
        }
        
        var engine = module.CreateMCJITCompiler();
        var main = module.GetNamedFunction("main");
        var result = engine.RunFunction(main, []);
        
        unsafe
        {
            var exitCode = unchecked((int)LLVM.GenericValueToInt(result, 0));
            return exitCode;
        }
    }
}