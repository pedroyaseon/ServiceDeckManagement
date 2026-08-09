namespace ServiceDeckManagement.HostTests;

public sealed class ServiceHostArgumentsTests
{
    [Fact]
    public void TryParse_AcceptsOnlyCanonicalServiceId()
    {
        var parsed = ServiceHostArguments.TryParse(
            ["--service-id", "example-api"],
            out var result,
            out var error);

        Assert.True(parsed);
        Assert.Equal("example-api", result!.ServiceId);
        Assert.Null(error);
    }

    [Theory]
    [InlineData()]
    [InlineData("--service-id")]
    [InlineData("--service-id", "Invalid")]
    [InlineData("--unknown", "example-api")]
    [InlineData("--service-id", "example-api", "extra")]
    public void TryParse_RejectsAmbiguousArguments(params string[] arguments)
    {
        Assert.False(ServiceHostArguments.TryParse(arguments, out var result, out var error));
        Assert.Null(result);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
