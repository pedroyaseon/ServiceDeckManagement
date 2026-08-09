using ServiceDeckManagement.Domain.Services;

namespace ServiceDeckManagement.UnitTests.Domain;

public sealed class ServiceIdTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("service-1")]
    [InlineData("api2026")]
    public void TryCreate_AcceptsCanonicalIdentifier(string value)
    {
        var created = ServiceId.TryCreate(value, out var serviceId);

        Assert.True(created);
        Assert.Equal(value, serviceId.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" Service")]
    [InlineData("service ")]
    [InlineData("Service")]
    [InlineData("service_name")]
    [InlineData("-service")]
    [InlineData("service-")]
    [InlineData("serviço")]
    public void TryCreate_RejectsNonCanonicalIdentifier(string value)
    {
        Assert.False(ServiceId.TryCreate(value, out _));
    }

    [Fact]
    public void TryCreate_RejectsIdentifierAboveMaximumLength()
    {
        Assert.False(ServiceId.TryCreate(new string('a', 64), out _));
    }
}
