using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Lexer;
using Byron.Compiler.SemanticAnalysis;

namespace Byron.Compiler.Tests.UnitTests.SymbolResolution;

public class ModuleTestFixtureBuilder
{
    public ModuleDeclarationNode Module { get; init; }
    public ModuleTestFixtureBuilder(ModuleDeclarationNode? module = null)
    {
        Module = module ?? new FileModuleNode(Symbol.Global, SourceSpan.Empty); 
    }

    public NominalTypeNode WithNominalType(string typeName)
    {
        var type = new NominalTypeNode(typeName, SourceSpan.Empty);
        Module.Declarations.Structs.Add(new StructDeclarationNode(type, [], type.Span));
        return type;
    }

    public TraitTypeNode WithTrait(string typeName)
    {
        var type = new TraitTypeNode(typeName, SourceSpan.Empty);
        Module.Declarations.Traits.Add(new TraitDeclarationNode(type, [], [], type.Span));
        return type;
    }
    
    public AliasDeclarationNode WithAlias(string aliasName, string targetPath)
    {
        var aliasNode = new AliasDeclarationNode(
            aliasName, 
            Symbol.From(targetPath),
            SourceSpan.Empty
        );
        Module.Declarations.Aliases.Add(aliasNode);
        return aliasNode;
    }
    
    public ImplementBlockDeclarationNode WithImplementBlock(NominalTypeNode associatedType, TraitTypeNode? associatedTrait) => new (associatedType, associatedTrait, SourceSpan.Empty);

    public FunctionDeclarationNode WithFunction(string name, ImplementBlockDeclarationNode? implementBlock = null)
    {
        var signature = new FunctionSignatureNode(name, [], new VoidTypeNode(SourceSpan.Empty), SourceSpan.Empty);
        var function = new FunctionDeclarationNode(signature, new BlockStatementNode([], SourceSpan.Empty), SourceSpan.Empty);

        if (implementBlock != null)
        {
            implementBlock.FunctionDeclarations.Add(function);
        }
        else
        {
            Module.Declarations.Functions.Add(function);
        }
        
        return function;
    }

    public ModuleTestFixtureBuilder WithChild(string name)
    {
        var module = new BlockModuleNode(name, SourceSpan.Empty);
        var builder = new ModuleTestFixtureBuilder(module);
        Module.Declarations.ChildModules.Add(module);
        return builder;
    }
}

public class SymbolResolutionTestFixtureBuilder(ModuleDeclarationNode module)
{
    public GlobalSymbolTableLookup Build()
    {
        var diagnostics = new Diagnostics();
        var symbols = new GlobalSymbolTable();
        symbols.Register([module], diagnostics);
        
        return new GlobalSymbolTableLookup(symbols);
    }
}