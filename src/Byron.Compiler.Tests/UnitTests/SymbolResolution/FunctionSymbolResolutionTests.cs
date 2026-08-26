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
        var canResolve = lookup.TryGetFunction(moduleBuilder.Module, [], function.Signature.Name, [], out var resolved);

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
        var canResolve = lookup.TryGetFunction(moduleBuilder.Module, type.Symbol.Segments, function.Signature.Name, [], out var resolved);

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, function.Signature.Name], resolved!.Symbol.Segments);
    }

    [Fact]
    public void Resolve_ResolvesStaticAssociatedFunctionWithSegments()
    {
        // Arrange
        var moduleBuilder = new ModuleTestFixtureBuilder();
        var type = moduleBuilder.WithNominalType("type");
        var block = moduleBuilder.WithImplementBlock(type);
        var function = moduleBuilder.WithFunction("myFunction", block);
        var lookup = new SymbolResolutionTestFixtureBuilder(moduleBuilder.Module).Build();
        
        // Act
        var canResolve = lookup.TryGetFunction(moduleBuilder.Module, [], function.Signature.Name, type.Symbol.Segments, out var resolved);

        // Assert
        Assert.True(canResolve);
        Assert.Equal([..type.Symbol.Segments, function.Signature.Name], resolved!.Symbol.Segments);
    }
}