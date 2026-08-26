using Byron.Compiler.AST.HighLevel;
using Byron.Compiler.Lexer;

namespace Byron.Compiler.Tests.UnitTests.SymbolResolution;

public class StructSymbolResolutionTests
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
        var canResolveType = lookup.TryResolveCanonicalType(module, type, [], out var resolvedType);
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
        var canResolveType = lookup.TryResolveCanonicalType(module, TypeNode(alias.Name), [], out var resolvedType);
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
        var canResolveType = lookup.TryResolveCanonicalType(module, TypeNode([alias.Name, "c"]), [], out var resolvedType);
        var canResolveStruct = lookup.TryGetStruct(resolvedType!, out var resolvedStruct);
        
        // Assert
        Assert.True(canResolveType);
        Assert.True(canResolveStruct);
        Assert.Equal(type.Symbol, resolvedType?.Symbol);
        Assert.Equal(resolvedStruct?.Type.Symbol, resolvedType?.Symbol);
    }

    [Fact]
    public void Resolve_ResolvesExactSymbol_PartialNamespaceOverlap()
    {        
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var subModule = moduleBuilder.WithChild("my.child");
        var type = subModule.WithNominalType("c");
        
        var lookup = new SymbolResolutionTestFixtureBuilder(moduleBuilder.Module).Build();
        
        // Act
        var canResolveType = lookup.TryResolveCanonicalType(subModule.Module, TypeNode(["child", "c"]), [], out var resolvedType);
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
        var resolved = lookup.TryResolveCanonicalType(module, first, [], out var resolvedType);
        
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
        var resolved = lookup.TryResolveCanonicalType(module, TypeNode(alias.Name), [], out var resolvedType);
        
        // Assert
        Assert.True(resolved);
        Assert.Equal(type.Symbol, resolvedType?.Symbol);
    }

    [Fact]
    public void Resolve_ResolvesSpecificSymbolFromAnotherScope_WhenSameTypeNameExistsInThisScope()
    {
        // Arrange
        var fileModule = new ModuleTestFixtureBuilder();

        var moduleA = fileModule.WithChild("a");  
        var moduleB = fileModule.WithChild("b"); 
        
        _ = moduleA.WithNominalType("c");
        var typeInModuleB = moduleB.WithNominalType("c");
        
        var lookup = new SymbolResolutionTestFixtureBuilder(fileModule.Module).Build();
        
        // Act
        var resolved = lookup.TryResolveCanonicalType(moduleA.Module, typeInModuleB, moduleA.Module.Symbol.Segments, out var resolvedType);
        
        // Assert
        Assert.True(resolved);
        Assert.Equal(typeInModuleB.Symbol, resolvedType?.Symbol);
    }

    [Fact]
    public void Resolve_ResolvesSpecificSymbolFromAnotherScope_WhenStructNameIsUnique()
    {
        // Arrange
        var fileModule = new ModuleTestFixtureBuilder();

        var moduleA = fileModule.WithChild("a");  
        var moduleB = fileModule.WithChild("b"); 
        
        var typeInModuleB = moduleB.WithNominalType("c");
        
        var lookup = new SymbolResolutionTestFixtureBuilder(fileModule.Module).Build();
        
        // Act
        var resolved = lookup.TryResolveCanonicalType(moduleA.Module, typeInModuleB, moduleA.Module.Symbol.Segments, out var resolvedType);
        
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
        var resolved = lookup.TryResolveCanonicalType(module.Module, new NominalTypeNode("c", SourceSpan.Empty), module.Module.Symbol.Segments,  out var resolvedType);
        
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
        var canResolveType = lookup.TryResolveCanonicalType(moduleB.Module, typeInModuleA, moduleA.Module.Symbol.Segments, out var resolvedType);
        var canResolveStruct = lookup.TryGetStruct(typeInModuleA, out var resolvedTrait);
        
        // Assert
        Assert.True(canResolveType);
        Assert.True(canResolveStruct);
        Assert.Equal(typeInModuleA.Symbol, resolvedType?.Symbol);
    }
}
