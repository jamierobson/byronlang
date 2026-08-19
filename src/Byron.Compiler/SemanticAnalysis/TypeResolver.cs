// using System.Diagnostics.CodeAnalysis;
// using Byron.Compiler.AST;
// using Byron.Compiler.AST.HighLevel;
// using Byron.Compiler.Lexer;
//
// namespace Byron.Compiler.SemanticAnalysis;
//
// public enum ResolutionState
// {
//     Unresolved,
//     Resolving,
//     Resolved
// }
//
// public class ResolutionLookups
// {
//     private readonly Dictionary<string, StructDeclarationNode> _declarations = new();
//     private readonly Dictionary<string, ResolutionState> _resolutionStates = new();
//
//     public void Add(StructDeclarationNode structDeclaration)
//     {
//         var canonicalName = structDeclaration.Symbol.ToString();
//         _declarations.Add(canonicalName, structDeclaration);
//         _resolutionStates[canonicalName] = ResolutionState.Unresolved;
//     }
//     
//     public IReadOnlyDictionary<string, StructDeclarationNode> Declarations => _declarations;
//     public StructDeclarationNode GetDeclaration(AST.Symbol symbol) => _declarations[symbol.ToString()];
//     public StructDeclarationNode GetDeclaration(string canonicalName) => _declarations[canonicalName];
//     public bool TryGetDeclaration(AST.Symbol symbol, [NotNullWhen(true)] out StructDeclarationNode? declaration) => _declarations.TryGetValue(symbol.ToString(), out declaration);
//     public ResolutionState GetState(AST.Symbol symbol) => _resolutionStates[symbol.ToString()];
//     public void SetState(AST.Symbol symbol, ResolutionState state) => _resolutionStates[symbol.ToString()] = state;
//     public bool TryGetResolutionState(string canonicalName, out ResolutionState resolutionState) => _resolutionStates.TryGetValue(canonicalName, out resolutionState); 
//     public bool TryGetResolutionState(AST.Symbol symbol, out ResolutionState resolutionState) => _resolutionStates.TryGetValue(symbol.ToString(), out resolutionState); 
// }
//
// public class TypeResolver
// {
//     private readonly TypeRegistry _typeRegistry;
//
//     private readonly ResolutionLookups _resolutionLookups = new();
//     // private readonly Dictionary<string, StructDeclarationNode> _declarations = new();
//     // private readonly Dictionary<string, ResolutionState> _resolutionStates = new();
//     private readonly Diagnostics _diagnostics;
//
//     public TypeResolver(
//         TypeRegistry typeRegistry, 
//         IEnumerable<StructDeclarationNode> structDeclarations, 
//         Diagnostics diagnostics)
//     {
//         _typeRegistry = typeRegistry;
//         _diagnostics = diagnostics;
//
//         foreach (var structDeclarationNode in structDeclarations)
//         {
//             _resolutionLookups.Add(structDeclarationNode);
//             // var canonicalName = structDeclarationNode.CanonicalName.ToString();
//             // _declarations.Add(canonicalName, structDeclarationNode);
//             // _resolutionStates[canonicalName] = ResolutionState.Unresolved;
//         }
//     }
//
//     public void Resolve()
//     {
//         foreach (var declaration in _resolutionLookups.Declarations)
//         {
//             _ = EnsureResolved(declaration);
//         }
//     }
//
//     private bool EnsureResolved(KeyValuePair<string, StructDeclarationNode> declaration)
//     {
//         if (_resolutionLookups.TryGetResolutionState(declaration.Key, out var state) && state == ResolutionState.Resolved)
//         {
//             return true;
//         }
//         
//         foreach (var field in declaration.Value.Fields)
//         {
//             if (!EnsureResolved(field.Type))
//             {
//                 return false;
//             }
//         }
//
//         if (_typeRegistry.IsValidStructName(declaration.Value.Name) && _typeRegistry.IsValidStructName(declaration.Key))
//         {
//             _resolutionLookups.SetState(declaration.Value.Symbol, ResolutionState.Resolving);
//             // _resolutionStates[declaration.Key] = ResolutionState.Resolved;
//             if (_typeRegistry.TryRegister(declaration.Value))
//             {
//                 return true;
//             }
//             
//             _ = _typeRegistry.TryGetStruct(declaration.Value.Name, out var duplicateDeclaration);
//             
//             _diagnostics.Duplicate(declaration.Value, duplicateDeclaration!.Span);
//             return false;
//         }
//         _diagnostics.InvalidStructName(declaration.Value, declaration.Key);
//         return false;
//     }
//
//     private bool EnsureResolved(TypeNode typeNode) => EnsureResolved(typeNode, typeNode.Span);
//     
//     private bool EnsureResolved(TypeNode typeNode, SourceSpan sourceSpan)
//     {
//         if (_typeRegistry.IsValidType(typeNode))
//         {
//             return true;
//         }
//
//         if (typeNode is ReferenceTypeNode referenceTypeNode)
//         {
//             return EnsureResolved(referenceTypeNode.Target, sourceSpan);
//         }
//
//         var canonicalName =  typeNode.Symbol;
//         if (!_resolutionLookups.TryGetDeclaration(canonicalName, out var structDeclaration))
//         {
//             _diagnostics.UndeclaredType(typeNode);
//             return false;
//         }
//
//         _ = _resolutionLookups.TryGetResolutionState(canonicalName, out var state);
//         
//         // todo: We should use the return value here
//
//         if (state == ResolutionState.Resolved)
//         {
//             return true;
//         }
//
//         if (state == ResolutionState.Resolving)
//         {
//             _diagnostics.CircularReference(canonicalName, sourceSpan);
//             return false;
//         }
//
//         _resolutionLookups.SetState(canonicalName, ResolutionState.Resolving);
//
//         var hasErrors = false;
//         foreach (var field in structDeclaration.Fields)
//         {
//             if (!EnsureResolved(field.Type))
//             {
//                 hasErrors = true;
//             }
//         }
//
//         if (hasErrors)
//         {
//             return false;
//         }
//
//         if (_typeRegistry.IsValidStructName(structDeclaration.Name) && _typeRegistry.IsValidStructName(canonicalName))
//         {
//             _resolutionLookups.SetState(canonicalName, ResolutionState.Resolved);
//
//             if (_typeRegistry.TryRegister(structDeclaration))
//             {
//                 return true;
//             }
//             
//             _ = _typeRegistry.TryGetStruct(canonicalName, out var duplicateDeclaration);
//             
//             _diagnostics.Duplicate(structDeclaration, duplicateDeclaration!.Span);
//             return false;
//
//         }
//         
//         _diagnostics.InvalidStructName(structDeclaration, canonicalName.ToString());
//         return false;
//     }
// }