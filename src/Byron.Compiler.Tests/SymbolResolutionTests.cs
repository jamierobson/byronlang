using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Lexer;

namespace Byron.Compiler.Tests;

public class SymbolResolutionTests
{
    private static TypeNode TypeNode(string name) => TypeNode([name]);
    private static TypeNode TypeNode(string[] name) => new NominalTypeNode(name, SourceSpan.Empty); 
    
    [Fact]
    public void Resolve_ResolvesExactSymbol()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var type = moduleBuilder.WithNominalType("a.b.c");
        var module = moduleBuilder.Module;
        var lookup = new SymbolResolutionTestFixtureBuilder(module).Build();
        
        // Act
        var canResolveType = lookup.TryResolveCanonicalType(type, [], module, out var resolvedType);
        var canResolveStruct = lookup.TryGetStruct(type, out var resolvedStruct);
        
        // Assert
        Assert.True(canResolveType);
        Assert.True(canResolveStruct);
        Assert.Equal(type.Symbol, resolvedType?.Symbol);
        Assert.Equal(resolvedStruct?.Type.Symbol, resolvedType?.Symbol);
    }
    
    [Fact]
    public void Resolve_ResolvesExactSymbol_WithDirectAlias()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var type = moduleBuilder.WithNominalType("a.b.c");
        var alias = moduleBuilder.WithAlias("alias", type.Symbol.ToString());
        
        var module = moduleBuilder.Module;
        var lookup = new SymbolResolutionTestFixtureBuilder(module).Build();
        
        // Act
        var canResolveType = lookup.TryResolveCanonicalType(TypeNode(alias.Name), [], module, out var resolvedType);
        var canResolveStruct = lookup.TryGetStruct(resolvedType!, out var resolvedStruct);
        
        // Assert
        Assert.True(canResolveType);
        Assert.True(canResolveStruct);
        Assert.Equal(type.Symbol, resolvedType?.Symbol);
        Assert.Equal(resolvedStruct?.Type.Symbol, resolvedType?.Symbol);
    }
    
    [Fact]
    public void Resolve_ResolvesExactSymbol_ParentPathAlias()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var type = moduleBuilder.WithNominalType("a.b.c");
        var alias = moduleBuilder.WithAlias("alias", "a.b");
        
        var module = moduleBuilder.Module;
        var lookup = new SymbolResolutionTestFixtureBuilder(module).Build();
        
        // Act
        var canResolveType = lookup.TryResolveCanonicalType(TypeNode([alias.Name, "c"]), [], module, out var resolvedType);
        var canResolveStruct = lookup.TryGetStruct(resolvedType!, out var resolvedStruct);
        
        // Assert
        Assert.True(canResolveType);
        Assert.True(canResolveStruct);
        Assert.Equal(type.Symbol, resolvedType?.Symbol);
        Assert.Equal(resolvedStruct?.Type.Symbol, resolvedType?.Symbol);
    }

    [Fact]
    public void Resolve_ResolvesSpecificSymbolWhenTwoCandidatesExist()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var first = moduleBuilder.WithNominalType("a.b.c");
        _ = moduleBuilder.WithNominalType("a.c");
        var module = moduleBuilder.Module;
        var lookup = new SymbolResolutionTestFixtureBuilder(module).Build();
        
        // Act
        var resolved = lookup.TryResolveCanonicalType(first, [], module, out var resolvedType);
        
        // Assert
        Assert.True(resolved);
        Assert.Equal(first.Symbol, resolvedType?.Symbol);
    }
    
    [Fact]
    public void Resolve_ResolvesExactSymbol_WithDirectAlias_WhenAnotherCandidateExists()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var type = moduleBuilder.WithNominalType("a.b.c");
        _ = moduleBuilder.WithNominalType("a.b.c.c");
        var alias = moduleBuilder.WithAlias("alias", type.Symbol.ToString());
        
        var module = moduleBuilder.Module;
        var lookup = new SymbolResolutionTestFixtureBuilder(module).Build();
        
        // Act
        var resolved = lookup.TryResolveCanonicalType(TypeNode(alias.Name), [], module, out var resolvedType);
        
        // Assert
        Assert.True(resolved);
        Assert.Equal(type.Symbol, resolvedType?.Symbol);
    }

    [Fact]
    public void Resolve_ResolvesSpecificSymbolFromAnotherScope()
    {
        // Arrange
        var fileModule = new ModuleTestFixtureBuilder();

        var moduleA = fileModule.WithChild("a");  
        var moduleB = fileModule.WithChild("b"); 
        
        _ = moduleA.WithNominalType("c");
        var typeInModuleB = moduleB.WithNominalType("c");
        
        var lookup = new SymbolResolutionTestFixtureBuilder(fileModule.Module).Build();
        
        // Act
        var resolved = lookup.TryResolveCanonicalType(typeInModuleB, moduleA.Module.Symbol.Segments, moduleA.Module, out var resolvedType);
        
        // Assert
        Assert.True(resolved);
        Assert.Equal(typeInModuleB.Symbol, resolvedType?.Symbol);
    }

    [Fact]
    public void Resolve_ResolvesLocalSymbolWhenNotFullyQualified()
    {
        // Arrange
        var fileModule = new ModuleTestFixtureBuilder();
        var module = fileModule.WithChild("a");  
        var type = module.WithNominalType("c");
        
        var lookup = new SymbolResolutionTestFixtureBuilder(fileModule.Module).Build();
        
        // Act
        var resolved = lookup.TryResolveCanonicalType(new TraitTypeNode("c", SourceSpan.Empty), module.Module.Symbol.Segments, module.Module, out var resolvedType);
        
        // Assert
        Assert.True(resolved);
        Assert.Equal(type.Symbol, resolvedType?.Symbol);
    }

    [Fact]
    public void Resolve_ResolvesLocalSymbolOverOtherScopeWhenNotFullyQualified()
    {
        // Arrange
        var fileModule = new ModuleTestFixtureBuilder();

        var moduleA = fileModule.WithChild("a");  
        var moduleB = fileModule.WithChild("b"); 
        
        var typeInModuleA = moduleA.WithNominalType("c");
        _ = moduleB.WithNominalType("c");
        
        var lookup = new SymbolResolutionTestFixtureBuilder(fileModule.Module).Build();
        
        // Act
        // var traitTypeNode = new TraitTypeNode("c", SourceSpan.Empty);
        var canResolveType = lookup.TryResolveCanonicalType(typeInModuleA, moduleA.Module.Symbol.Segments, moduleB.Module, out var resolvedType);
        var canResolveStruct = lookup.TryGetStruct(typeInModuleA, out var resolvedTrait);
        
        // Assert
        Assert.True(canResolveType);
        Assert.True(canResolveStruct);
        Assert.Equal(typeInModuleA.Symbol, resolvedType?.Symbol);
    }
}
