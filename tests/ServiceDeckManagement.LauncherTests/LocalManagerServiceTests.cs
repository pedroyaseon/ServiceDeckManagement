using System.Text.Json;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Infrastructure.LocalProtocol;
using ServiceDeckManagement.Launcher;

namespace ServiceDeckManagement.LauncherTests;

public sealed class LocalManagerServiceTests
{
    [Fact]
    public async Task Inventory_UsesLocalManagerWithoutApiSession()
    {
        var client = new RecordingClient((operation, _, actorId, actorRole) =>
        {
            Assert.Equal(ManagerOperationsV1.Inventory, operation);
            Assert.Equal("launcher.local", actorId);
            Assert.Equal("administrator", actorRole);
            return Success(Array.Empty<ManagedServiceSnapshotV1>());
        });
        var service = new LocalManagerService(client);

        var inventory = await service.GetServicesAsync(CancellationToken.None);

        Assert.Empty(inventory);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Start_SendsTypedServiceIdentifier()
    {
        var client = new RecordingClient((operation, payload, _, _) =>
        {
            Assert.Equal(ManagerOperationsV1.Start, operation);
            var serviceId = Assert.IsType<ServiceIdPayloadV1>(payload);
            Assert.Equal("api-interna", serviceId.ServiceId);
            return Success();
        });
        var service = new LocalManagerService(client);

        await service.StartServiceAsync("api-interna", CancellationToken.None);

        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task DeniedRequest_UsesStableMessageWithoutManagerDetails()
    {
        var client = new RecordingClient((_, _, _, _) => new ManagerResponseV1
        {
            ProtocolVersion = 1,
            RequestId = Guid.NewGuid().ToString("D"),
            Success = false,
            ErrorCode = "request.denied",
            Message = "detalhe interno que não deve chegar à interface"
        });
        var service = new LocalManagerService(client);

        var exception = await Assert.ThrowsAsync<LocalManagerException>(
            () => service.GetServicesAsync(CancellationToken.None));

        Assert.Equal("O usuário do Windows não está autorizado no Manager.", exception.Message);
        Assert.DoesNotContain("detalhe interno", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidPayload_IsRejected()
    {
        var client = new RecordingClient((_, _, _, _) => Success(new { unexpected = true }));
        var service = new LocalManagerService(client);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.GetServicesAsync(CancellationToken.None));
    }

    private static ManagerResponseV1 Success(object? data = null) => new()
    {
        ProtocolVersion = 1,
        RequestId = Guid.NewGuid().ToString("D"),
        Success = true,
        Data = data is null ? null : JsonSerializer.SerializeToElement(data, ManagerJson.Options)
    };

    private sealed class RecordingClient(
        Func<string, object, string, string, ManagerResponseV1> response) : IManagerClient
    {
        public int Calls { get; private set; }

        public Task<ManagerResponseV1> SendAsync(
            string operation,
            object payload,
            string actorId,
            string actorRole,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(response(operation, payload, actorId, actorRole));
        }
    }
}
