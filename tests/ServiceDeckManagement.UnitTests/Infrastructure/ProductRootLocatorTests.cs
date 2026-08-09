using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.UnitTests.Infrastructure;

public sealed class ProductRootLocatorTests
{
    [Fact]
    public void RootPath_FindsValidMarkerFromNestedDirectory()
    {
        using var directory = TemporaryDirectory.Create();
        var nested = Path.Combine(directory.Path, "app", "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllLines(
            Path.Combine(directory.Path, ProductRootLocator.MarkerFileName),
            ["schemaVersion=1", "product=ServiceDeckManagement"]);

        var locator = new ProductRootLocator(nested);

        Assert.Equal(directory.Path, locator.RootPath);
    }

    [Fact]
    public void RootPath_RejectsMarkerFromAnotherProduct()
    {
        using var directory = TemporaryDirectory.Create();
        File.WriteAllLines(
            Path.Combine(directory.Path, ProductRootLocator.MarkerFileName),
            ["schemaVersion=1", "product=OtherProduct"]);

        var locator = new ProductRootLocator(directory.Path);

        Assert.Throws<ProductRootNotFoundException>(() => locator.RootPath);
    }

    [Fact]
    public void RootPath_FailsWhenMarkerDoesNotExist()
    {
        using var directory = TemporaryDirectory.Create();
        var locator = new ProductRootLocator(directory.Path);

        Assert.Throws<ProductRootNotFoundException>(() => locator.RootPath);
    }
}
