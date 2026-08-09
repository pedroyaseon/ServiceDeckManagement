using System.Diagnostics;
using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.HostTests;

public sealed class ServiceHostProgramIntegrationTests
{
    [Fact]
    public async Task Executable_RunsDefinitionAndProducesSanitizedPortableLog()
    {
        using var root = TemporaryProductRoot.Create();
        var service = TestServiceDefinition.Create(
            root,
            "--emit",
            "argument with spaces");
        var definitions = Path.Combine(root.RootPath, "config", "services");
        Directory.CreateDirectory(definitions);
        await File.WriteAllTextAsync(
            Path.Combine(definitions, "test-service.json"),
            ServiceDefinitionJson.Serialize(service.Definition),
            new System.Text.UTF8Encoding(false));
        var hostPath = root.CopyHostApplication();
        using var host = Process.Start(new ProcessStartInfo
        {
            FileName = hostPath,
            WorkingDirectory = Path.GetDirectoryName(hostPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "--service-id", "test-service" }
        }) ?? throw new InvalidOperationException("Falha ao iniciar o Host de teste.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await host.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                await host.WaitForExitAsync(CancellationToken.None);
            }
        }

        Assert.Equal(7, host.ExitCode);
        var logPath = Path.Combine(
            root.RootPath,
            "logs",
            "services",
            "test-service",
            "service.log");
        var content = await File.ReadAllTextAsync(logPath);
        Assert.Contains("saída limpa", content, StringComparison.Ordinal);
        Assert.Contains("argument with spaces", content, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', content);
        Assert.DoesNotContain('\ufffd', content);
    }
}
