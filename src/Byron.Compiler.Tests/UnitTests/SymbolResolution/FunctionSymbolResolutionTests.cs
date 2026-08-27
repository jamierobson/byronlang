using Byron.Compiler.AST;

namespace Byron.Compiler.Tests.UnitTests.SymbolResolution;

public class FunctionSymbolResolutionTests
{
    [Fact]
    public void Resolve_ResolvesLocalFreeFunction()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var function = moduleBuilder.WithFunction("myFunction");
        var lookup = new SymbolResolutionTestFixtureBuilder(moduleBuilder.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            moduleBuilder.Module, 
            Symbol.From(function.Signature.Name), 
            function.Signature.Name, 
            [], 
            out var resolved);

        // Assert
        Assert.True(canResolve);
        Assert.Equal(function.Signature, resolved!.Signature);
    }

    [Fact]
    public void Resolve_ResolvesStaticAssociatedFunctionWithType()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var type = moduleBuilder.WithNominalType("type");
        var block = moduleBuilder.WithImplementBlock(type);
        var function = moduleBuilder.WithFunction("myFunction", block);
        var lookup = new SymbolResolutionTestFixtureBuilder(moduleBuilder.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            moduleBuilder.Module, 
            Symbol.From([..type.Symbol.Segments, function.Signature.Name]),
            function.Signature.Name, 
            [],
            out var resolved);

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, function.Signature.Name], resolved!.Symbol.Segments);
    }

    // [Fact]
    // public void Resolve_ResolvesStaticAssociatedFunctionWithSegments()
    // {
    //     // Arrange
    //     var moduleBuilder = new ModuleTestFixtureBuilder();
    //     var type = moduleBuilder.WithNominalType("type");
    //     var block = moduleBuilder.WithImplementBlock(type);
    //     var function = moduleBuilder.WithFunction("myFunction", block);
    //     var lookup = new SymbolResolutionTestFixtureBuilder(moduleBuilder.Module).Build();
    //
    //     // Act
    //     var canResolve = lookup.TryGetFunction(
    //         moduleBuilder.Module, 
    //         [], 
    //         function.Signature.Name, 
    //         type.Symbol.Segments,
    //         out var resolved);
    //
    //     // Assert
    //     Assert.True(canResolve);
    //     Assert.Equal([..type.Symbol.Segments, function.Signature.Name], resolved!.Symbol.Segments);
    // }

    [Fact]
    public void Resolve_ResolvesLocalFreeFunctionThroughAlias()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var nested = moduleBuilder.WithChild("nested");
        var function = nested.WithFunction("myFunction");
        moduleBuilder.WithAlias("Alias", nested.Module.Symbol.ToString());
        var lookup = new SymbolResolutionTestFixtureBuilder(moduleBuilder.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            moduleBuilder.Module, 
            Symbol.From(["Alias", function.Signature.Name]), 
            function.Signature.Name, 
            [],
            out var resolved);

        // Assert
        Assert.True(canResolve);
        Assert.Equal(function.Signature, resolved!.Signature);
    }

    [Fact]
    public void Resolve_ResolvesStaticAssociatedFunctionThroughAliasedType()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var type = moduleBuilder.WithNominalType("type");
        var block = moduleBuilder.WithImplementBlock(type);
        var function = moduleBuilder.WithFunction("myFunction", block);
        moduleBuilder.WithAlias("AliasedType", type.Symbol.ToString());
        var lookup = new SymbolResolutionTestFixtureBuilder(moduleBuilder.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            moduleBuilder.Module, 
            Symbol.From(["AliasedType",  function.Signature.Name]), 
            function.Signature.Name, 
            [],
            out var resolved);

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, function.Signature.Name], resolved!.Symbol.Segments);
    }

    [Fact]
    public void Resolve_ResolvesStaticAssociatedFunctionThroughChainedAlias()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var type = moduleBuilder.WithNominalType("type");
        var block = moduleBuilder.WithImplementBlock(type);
        var function = moduleBuilder.WithFunction("myFunction", block);
        moduleBuilder.WithAlias("InnerAlias", type.Symbol.ToString());
        moduleBuilder.WithAlias("OuterAlias", "InnerAlias");
        var lookup = new SymbolResolutionTestFixtureBuilder(moduleBuilder.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            moduleBuilder.Module, 
            Symbol.From(["OuterAlias", function.Signature.Name]), 
            function.Signature.Name, 
            [],
            out var resolved);

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, function.Signature.Name], resolved!.Symbol.Segments);
    }

    [Fact]
    public void Resolve_ResolvesFullyQualifiedTraitFunction()
    {
        // Arrange
        var root = new ModuleTestFixtureBuilder();
        var moduleA = root.WithChild("My.ModuleA");
        var moduleB = root.WithChild("My.ModuleB");

        var type = moduleA.WithNominalType("Dupe");
        var trait = moduleB.WithTrait("MyTrait");
        var block = moduleA.WithImplementBlock(type, trait);
        var function = moduleA.WithFunction("get", block);

        var lookup = new SymbolResolutionTestFixtureBuilder(root.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            root.Module,
            Symbol.From(["My", "ModuleA", "Dupe", "My", "ModuleB", "MyTrait", function.Signature.Name]),
            function.Signature.Name,
            [],
            out var resolved
        );

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, ..trait.Symbol.Segments, function.Signature.Name],
            resolved!.Symbol.Segments);
    }

    [Fact]
    public void Resolve_ResolvesAliasedTypeWithFullTraitFunction()
    {
        // Arrange
        var root = new ModuleTestFixtureBuilder();
        var moduleA = root.WithChild("My.ModuleA");
        var moduleB = root.WithChild("My.ModuleB");

        var type = moduleA.WithNominalType("Dupe");
        var trait = moduleB.WithTrait("MyTrait");
        var block = moduleA.WithImplementBlock(type, trait);
        var function = moduleA.WithFunction("get", block);

        root.WithAlias("DupeA", "My.ModuleA.Dupe");

        var lookup = new SymbolResolutionTestFixtureBuilder(root.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            root.Module,
            Symbol.From(["DupeA", "My", "ModuleB", "MyTrait", function.Signature.Name]),
            function.Signature.Name,
            [],
            out var resolved
        );

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, ..trait.Symbol.Segments, function.Signature.Name],
            resolved!.Symbol.Segments);
    }

    [Fact]
    public void Resolve_ResolvesFullTypeWithAliasedTraitFunction()
    {
        // Arrange
        var root = new ModuleTestFixtureBuilder();
        var moduleA = root.WithChild("My.ModuleA");
        var moduleB = root.WithChild("My.ModuleB");

        var type = moduleA.WithNominalType("Dupe");
        var trait = moduleB.WithTrait("MyTrait");
        var block = moduleA.WithImplementBlock(type, trait);
        var function = moduleA.WithFunction("get", block);

        root.WithAlias("AliasedTrait", "My.ModuleB.MyTrait");

        var lookup = new SymbolResolutionTestFixtureBuilder(root.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            root.Module,
            Symbol.From(["My", "ModuleA", "Dupe", "AliasedTrait", function.Signature.Name]),
            function.Signature.Name,
            [],
            out var resolved
        );

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, ..trait.Symbol.Segments, function.Signature.Name],
            resolved!.Symbol.Segments);
    }

    [Fact]
    public void Resolve_ResolvesAliasedTypeAndAliasedTraitFunction()
    {
        // Arrange
        var root = new ModuleTestFixtureBuilder();
        var moduleA = root.WithChild("My.ModuleA");
        var moduleB = root.WithChild("My.ModuleB");

        var type = moduleA.WithNominalType("Dupe");
        var trait = moduleB.WithTrait("MyTrait");
        var block = moduleA.WithImplementBlock(type, trait);
        var function = moduleA.WithFunction("get", block);

        root.WithAlias("DupeA", "My.ModuleA.Dupe");
        root.WithAlias("AliasedTrait", "My.ModuleB.MyTrait");

        var lookup = new SymbolResolutionTestFixtureBuilder(root.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            root.Module,
            Symbol.From(["DupeA", "AliasedTrait", function.Signature.Name]),
            function.Signature.Name,
            [],
            out var resolved
        );

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, ..trait.Symbol.Segments, function.Signature.Name],
            resolved!.Symbol.Segments);
    }

    [Fact]
    public void Resolve_ResolvesAliasedModuleTypeAndTraitFunction()
    {
        // Arrange
        var root = new ModuleTestFixtureBuilder();
        var moduleB = root.WithChild("My.ModuleB");

        var type = moduleB.WithNominalType("Dupe");
        var trait = moduleB.WithTrait("MyTrait");
        var block = moduleB.WithImplementBlock(type, trait);
        var function = moduleB.WithFunction("get", block);

        root.WithAlias("Module_B", "My.ModuleB");

        var lookup = new SymbolResolutionTestFixtureBuilder(root.Module).Build();

        // Act
        var canResolve = lookup.TryGetFunction(
            root.Module,
            Symbol.From(["Module_B", "Dupe", "Module_B", "MyTrait", function.Signature.Name]),
            function.Signature.Name,
            [],
            out var resolved
        );

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, ..trait.Symbol.Segments, function.Signature.Name],
            resolved!.Symbol.Segments);
    }
}