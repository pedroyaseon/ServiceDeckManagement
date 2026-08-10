using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Versioning;

namespace ServiceDeckManagement.Infrastructure.LocalProtocol;

public interface IManagerClient
{
    Task<ManagerResponseV1> SendAsync(
        string operation,
        object payload,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken);
}

public sealed class NamedPipeManagerClient(ITransportKeyProvider keyProvider) : IManagerClient
{
    public async Task<ManagerResponseV1> SendAsync(
        string operation,
        object payload,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var pipe = new NamedPipeClientStream(
            ".",
            ContractVersions.ManagerPipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Impersonation);
        await pipe.ConnectAsync(1_000, timeout.Token).ConfigureAwait(false);

        var key = await keyProvider.GetKeyAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var challenge = ManagerJson.Deserialize<ManagerChallengeV1>(
                await LengthPrefixedFrame.ReadAsync(pipe, timeout.Token).ConfigureAwait(false));
            var authentication = ManagerHandshake.CreateAuthentication(key, challenge);
            await LengthPrefixedFrame.WriteAsync(
                pipe, ManagerJson.Serialize(authentication), timeout.Token).ConfigureAwait(false);
            var request = new ManagerRequestV1
            {
                ProtocolVersion = ContractVersions.LocalProtocol,
                RequestId = Guid.NewGuid().ToString("D"),
                Operation = operation,
                ActorId = actorId,
                ActorRole = actorRole,
                Payload = JsonSerializer.SerializeToElement(payload, ManagerJson.Options)
            };
            await LengthPrefixedFrame.WriteAsync(
                pipe, ManagerJson.Serialize(request), timeout.Token).ConfigureAwait(false);
            var response = ManagerJson.Deserialize<ManagerResponseV1>(
                await LengthPrefixedFrame.ReadAsync(pipe, timeout.Token).ConfigureAwait(false));
            if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal) ||
                response.ProtocolVersion != ContractVersions.LocalProtocol)
            {
                throw new InvalidDataException("A resposta do Manager não corresponde à requisição.");
            }

            return response;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
