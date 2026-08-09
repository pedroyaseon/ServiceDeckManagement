using System.Net;
using System.Net.Sockets;
using System.Text;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Host.Health;
using ServiceDeckManagement.Host.Processes;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.HostTests;

public sealed class HealthProbeTests
{
    [Fact]
    public async Task TcpProbe_ConnectsOnlyToConfiguredLoopbackEndpoint()
    {
        using var root = TemporaryProductRoot.Create();
        await using var managed = StartWaitingProcess(root);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();
        await using var probe = HealthProbeFactory.Create(new HealthCheckV1
        {
            Type = "tcp",
            Target = $"127.0.0.1:{port}",
            IntervalSeconds = 1,
            TimeoutSeconds = 1
        });

        var healthy = await probe.CheckAsync(managed, CancellationToken.None);
        using var accepted = await acceptTask;

        Assert.True(healthy);
    }

    [Fact]
    public async Task HttpProbe_AcceptsSuccessfulLoopbackResponse()
    {
        using var root = TemporaryProductRoot.Create();
        await using var managed = StartWaitingProcess(root);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = ServeSingleResponseAsync(listener);
        await using var probe = HealthProbeFactory.Create(new HealthCheckV1
        {
            Type = "http",
            Target = $"http://127.0.0.1:{port}/health",
            IntervalSeconds = 1,
            TimeoutSeconds = 1
        });

        var healthy = await probe.CheckAsync(managed, CancellationToken.None);
        await serverTask;

        Assert.True(healthy);
    }

    private static ManagedProcess StartWaitingProcess(TemporaryProductRoot root)
    {
        var service = TestServiceDefinition.Create(root, "--wait");
        var factory = new ManagedProcessFactory(
            new ProcessStartInfoFactory(new PortablePathResolver(root)),
            new CollectingLogSink());
        return factory.Start(service);
    }

    private static async Task ServeSingleResponseAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        using var reader = new StreamReader(
            stream,
            Encoding.ASCII,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync()))
        {
            // Consome os cabeçalhos antes de responder.
        }

        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response);
        await stream.FlushAsync();
    }
}
