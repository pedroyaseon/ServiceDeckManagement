using ServiceDeckManagement.Host.Processes;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.HostTests;

public sealed class ProcessStartInfoFactoryTests
{
    [Fact]
    public void Create_UsesDirectExecutionAndSeparatedArguments()
    {
        using var root = TemporaryProductRoot.Create();
        var service = TestServiceDefinition.Create(
            root,
            "--emit",
            "value with spaces",
            "& whoami");
        var resolver = new PortablePathResolver(root);
        var factory = new ProcessStartInfoFactory(resolver);

        var result = factory.Create(service);

        Assert.False(result.UseShellExecute);
        Assert.True(result.RedirectStandardOutput);
        Assert.True(result.RedirectStandardError);
        Assert.Equal(service.ExecutablePath, result.FileName);
        Assert.Equal(service.WorkingDirectoryPath, result.WorkingDirectory);
        Assert.Equal(service.Definition.Arguments, result.ArgumentList);
        Assert.Equal("1", result.Environment["NO_COLOR"]);
        Assert.Equal("dumb", result.Environment["TERM"]);
    }
}
