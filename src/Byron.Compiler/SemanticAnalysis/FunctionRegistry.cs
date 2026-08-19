// using System.Diagnostics.CodeAnalysis;
// using Byron.Compiler.AST;
// using Byron.Compiler.AST.HighLevel;
//
// namespace Byron.Compiler.SemanticAnalysis;
//
// public class FunctionRegistry
// {
//     private readonly Dictionary<string, FunctionDescriptor> _declarations = [];
//     public IReadOnlyDictionary<string, FunctionDescriptor> Declarations => _declarations;
//     
//     public bool TryRegister(FunctionDeclarationNode declaration)
//     {
//         var canonicalName = declaration.Symbol.ToString();
//         var symbol = new FunctionDescriptor(
//             declaration.Symbol,
//             declaration.ModulePath,
//             declaration.Name,
//             declaration.Signature.Parameters.Select(p => new ParameterSymbol(p.Name, p.Type, p.Ownership)).ToList(),
//             declaration.Signature.ReturnType,
//             declaration
//         );
//
//         return _declarations.TryAdd(canonicalName, symbol);
//     }
//     
//     public bool TryGetFunction(string canonicalName, [NotNullWhen(true)] out FunctionDescriptor? function)
//     {
//         return _declarations.TryGetValue(canonicalName, out function);
//     }
//     
//     public bool TryGetFunctionInScope(
//         string[] modulePath, 
//         string shortName, 
//         [NotNullWhen(true)] out FunctionDescriptor? function)
//     {
//         return _declarations.TryGetValue(canonicalNameString, out function) 
//                || _declarations.TryGetValue(shortName, out function);
//     }
//
//     public string GetCanonicalName(FunctionDeclarationNode declaration)
//     {
//         // Leaving blank for now - we need to get lowering pass compilable before implementing here 
//     }
// }
