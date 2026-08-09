using ServiceDeckManagement.Domain.Services;

namespace ServiceDeckManagement.UnitTests.Domain;

public sealed class ManagedServiceNamesTests
{
    [Fact]
    public void FromId_ReturnsCanonicalScmName() =>
        Assert.Equal(
            "ServiceDeckManagement.Managed.sample",
            ManagedServiceNames.FromId("sample"));

    [Fact]
    public void FromId_RejectsInvalidIdentifier() =>
        Assert.Throws<ArgumentException>(() => ManagedServiceNames.FromId("Invalid"));
}
