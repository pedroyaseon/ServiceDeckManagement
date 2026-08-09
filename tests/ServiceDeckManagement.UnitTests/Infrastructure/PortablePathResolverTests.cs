using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.UnitTests.Infrastructure;

public sealed class PortablePathResolverTests
{
    [Fact]
    public void Resolve_AcceptsDescendantPath()
    {
        using var directory = TemporaryDirectory.Create();
        var resolver = new PortablePathResolver(
            new FixedRootProvider(directory.Path));

        var result = resolver.Resolve("apps/Example/Example.exe");

        Assert.True(result.IsValid);
        Assert.StartsWith(directory.Path, result.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../outside.exe")]
    [InlineData("apps/../../outside.exe")]
    public void Resolve_RejectsTraversal(string path)
    {
        using var directory = TemporaryDirectory.Create();
        var resolver = new PortablePathResolver(
            new FixedRootProvider(directory.Path));

        var result = resolver.Resolve(path);

        Assert.False(result.IsValid);
        Assert.Equal("path.outsideRoot", result.ErrorCode);
    }

    [Theory]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("\\\\server\\share\\app.exe")]
    [InlineData("//server/share/app.exe")]
    public void Resolve_RejectsAbsoluteOrNetworkPath(string path)
    {
        using var directory = TemporaryDirectory.Create();
        var resolver = new PortablePathResolver(
            new FixedRootProvider(directory.Path));

        var result = resolver.Resolve(path);

        Assert.False(result.IsValid);
        Assert.Equal("path.rooted", result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_RejectsEmptyPath(string? path)
    {
        using var directory = TemporaryDirectory.Create();
        var resolver = new PortablePathResolver(
            new FixedRootProvider(directory.Path));

        var result = resolver.Resolve(path);

        Assert.False(result.IsValid);
        Assert.Equal("path.required", result.ErrorCode);
    }

    [Theory]
    [InlineData("apps/../Example.exe")]
    [InlineData("apps/CON.exe")]
    [InlineData("apps/Example./Example.exe")]
    [InlineData("apps/Example:stream/Example.exe")]
    public void Resolve_RejectsAmbiguousOrReservedSegment(string path)
    {
        using var directory = TemporaryDirectory.Create();
        var resolver = new PortablePathResolver(
            new FixedRootProvider(directory.Path));

        var result = resolver.Resolve(path);

        Assert.False(result.IsValid);
        Assert.Equal("path.segment", result.ErrorCode);
    }

    [Fact]
    public void Resolve_RejectsPathAboveMaximumLength()
    {
        using var directory = TemporaryDirectory.Create();
        var resolver = new PortablePathResolver(
            new FixedRootProvider(directory.Path));

        var result = resolver.Resolve($"apps/{new string('a', 1_020)}.exe");

        Assert.False(result.IsValid);
        Assert.Equal("path.required", result.ErrorCode);
    }

    private sealed class FixedRootProvider(string rootPath)
        : IProductRootProvider
    {
        public string RootPath { get; } = rootPath;
    }
}
