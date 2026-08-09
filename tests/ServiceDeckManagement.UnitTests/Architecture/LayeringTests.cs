using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Contracts.Versioning;
using ServiceDeckManagement.Domain.Services;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.UnitTests.Architecture;

public sealed class LayeringTests
{
    [Fact]
    public void Domain_DoesNotReferenceOtherProductLayers()
    {
        var references = typeof(ServiceId).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith(
                "ServiceDeckManagement.",
                StringComparison.Ordinal) == true);

        Assert.Empty(references);
    }

    [Fact]
    public void Contracts_DoesNotReferenceOtherProductLayers()
    {
        var references = typeof(ContractVersions).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith(
                "ServiceDeckManagement.",
                StringComparison.Ordinal) == true);

        Assert.Empty(references);
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructure()
    {
        var references = typeof(IProductRootProvider).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.DoesNotContain("ServiceDeckManagement.Infrastructure", references);
    }

    [Fact]
    public void Infrastructure_ImplementsApplicationBoundary()
    {
        Assert.True(typeof(IProductRootProvider).IsAssignableFrom(
            typeof(ProductRootLocator)));
    }
}
