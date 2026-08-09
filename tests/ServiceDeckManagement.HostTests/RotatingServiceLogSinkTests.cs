using System.Text.Json;
using ServiceDeckManagement.Host.Logging;

namespace ServiceDeckManagement.HostTests;

public sealed class RotatingServiceLogSinkTests
{
    [Fact]
    public async Task WriteAsync_PersistsUtf8JsonWithoutTerminalSequences()
    {
        using var root = TemporaryProductRoot.Create();
        var service = TestServiceDefinition.Create(root, "--emit");
        await using var sink = new RotatingServiceLogSink(
            service,
            root,
            TimeProvider.System);

        await sink.WriteAsync(
            ServiceLogSource.StandardOutput,
            "\u001b[31mserviço saudável\u001b[0m\ufffd");

        var path = Path.Combine(
            root.RootPath,
            "logs",
            "services",
            "test-service",
            "service.log");
        var content = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(content);

        Assert.Equal(
            "serviço saudável",
            document.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain('\u001b', content);
        Assert.DoesNotContain('\ufffd', content);
    }

    [Fact]
    public async Task WriteAsync_RotatesAtConfiguredLimit()
    {
        using var root = TemporaryProductRoot.Create();
        var service = TestServiceDefinition.Create(root, "--emit");
        await using var sink = new RotatingServiceLogSink(
            service,
            root,
            TimeProvider.System);
        var payload = new string('x', 16_000);

        for (var index = 0; index < 70; index++)
        {
            await sink.WriteAsync(ServiceLogSource.StandardOutput, payload);
        }

        var directory = Path.Combine(
            root.RootPath,
            "logs",
            "services",
            "test-service");
        Assert.True(File.Exists(Path.Combine(directory, "service.log")));
        Assert.True(File.Exists(Path.Combine(directory, "service.001.log")));
        Assert.All(
            Directory.EnumerateFiles(directory),
            path => Assert.True(new FileInfo(path).Length <= 1_048_576));
    }

    [Fact]
    public async Task WriteAsync_DoesNotCreateFilesWhenLoggingIsDisabled()
    {
        using var root = TemporaryProductRoot.Create();
        var original = TestServiceDefinition.Create(root, "--emit");
        var service = original with
        {
            Definition = original.Definition with
            {
                Logging = original.Definition.Logging with { Enabled = false }
            }
        };
        await using var sink = new RotatingServiceLogSink(
            service,
            root,
            TimeProvider.System);

        await sink.WriteAsync(ServiceLogSource.System, "não persistir");

        Assert.False(Directory.Exists(Path.Combine(root.RootPath, "logs")));
    }
}
