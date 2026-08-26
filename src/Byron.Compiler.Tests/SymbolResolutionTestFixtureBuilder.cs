using Byron.Compiler.AST;
using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Lexer;
using Byron.Compiler.SemanticAnalysis;

namespace Byron.Compiler.Tests;

public class ModuleTestFixtureBuilder
{
    public ModuleDeclarationNode Module { get; init; }
    public ModuleTestFixtureBuilder(ModuleDeclarationNode? module = null)
    {
        Module = module ?? new FileModuleNode(Symbol.Global, SourceSpan.Empty); 
    }

    public NominalTypeNode WithType(string typeName)
    {
        var type = new NominalTypeNode(typeName, SourceSpan.Empty);
        Module.Declarations.Structs.Add(new StructDeclarationNode(type, [], type.Span));
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
        symbols.RegisterModules(module, [], diagnostics);
        symbols.RegisterAliasSymbols(module, diagnostics);
        symbols.RegisterTypeSymbols(module, [], diagnostics);
        symbols.BuildAliasContexts(module, [], diagnostics);
        
        return new GlobalSymbolTableLookup(symbols);
    }
}