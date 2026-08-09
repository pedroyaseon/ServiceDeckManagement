using System.Text.Json;
using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Versioning;
using ServiceDeckManagement.Domain.Manager;
using ServiceDeckManagement.Manager;

namespace ServiceDeckManagement.ManagerTests;

public sealed class ManagerRequestDispatcherAuthorizationTests
{
    private const string ActorId = "10000000-0000-0000-0000-000000000001";
    private static readonly ManagerClientIdentity ApiIdentity = new(
        "S-1-5-21-111111111-222222222-333333333-1001",
        ManagerRole.Viewer,
        IsApiClient: true);

    [Fact]
    public async Task ApiViewer_CannotDelegateStartOperation()
    {
        var dispatcher = new ManagerRequestDispatcher(null!, new EmptyLogReader());
        var request = CreateRequest(ManagerOperationsV1.Start, actorRole: "viewer");

        var response = await dispatcher.DispatchAsync(request, ApiIdentity, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("request.denied", response.ErrorCode);
    }

    [Fact]
    public async Task ApiClient_MustProvideKnownRoleAndGuidActor()
    {
        var dispatcher = new ManagerRequestDispatcher(null!, new EmptyLogReader());

        var unknownRole = await dispatcher.DispatchAsync(
            CreateRequest(ManagerOperationsV1.Ping, actorRole: "owner"), ApiIdentity, CancellationToken.None);
        var invalidActor = await dispatcher.DispatchAsync(
            CreateRequest(ManagerOperationsV1.Ping, actorRole: "viewer") with { ActorId = "not-a-guid" },
            ApiIdentity,
            CancellationToken.None);

        Assert.False(unknownRole.Success);
        Assert.False(invalidActor.Success);
    }

    [Fact]
    public async Task DirectAdministrator_IgnoresDelegatedIdentityFields()
    {
        var dispatcher = new ManagerRequestDispatcher(null!, new EmptyLogReader());
        var directAdministrator = new ManagerClientIdentity("S-1-5-18", ManagerRole.Administrator, IsApiClient: false);
        var request = CreateRequest(ManagerOperationsV1.Ping, actorRole: "invalid") with { ActorId = "invalid" };

        var response = await dispatcher.DispatchAsync(request, directAdministrator, CancellationToken.None);

        Assert.True(response.Success);
    }

    private static ManagerRequestV1 CreateRequest(string operation, string actorRole) => new()
    {
        ProtocolVersion = ContractVersions.LocalProtocol,
        RequestId = Guid.NewGuid().ToString("D"),
        Operation = operation,
        ActorId = ActorId,
        ActorRole = actorRole,
        Payload = JsonSerializer.SerializeToElement(new ServiceIdPayloadV1 { ServiceId = "sample" }, ManagerJson.Options)
    };

    private sealed class EmptyLogReader : IServiceLogReader
    {
        public Task<IReadOnlyList<ServiceLogEntryV1>> ReadAsync(
            string serviceId,
            long afterSequence,
            int limit,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ServiceLogEntryV1>>([]);
    }
}
