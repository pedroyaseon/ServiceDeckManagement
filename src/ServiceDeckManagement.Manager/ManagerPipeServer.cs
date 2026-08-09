using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Infrastructure.LocalProtocol;
using ServiceDeckManagement.Infrastructure.Security;

namespace ServiceDeckManagement.Manager;

public sealed class ManagerPipeServer(
    ManagerPipeFactory pipeFactory,
    ITransportKeyProvider keyProvider,
    ManagerRequestDispatcher dispatcher,
    ManagerSecurityOptions securityOptions)
{
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(15);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var active = new HashSet<Task>();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await DrainCompletedAsync(active).ConfigureAwait(false);
                if (active.Count >= ManagerPipeFactory.MaximumServerInstances)
                {
                    _ = await Task.WhenAny(active).ConfigureAwait(false);
                    continue;
                }

                var pipe = pipeFactory.Create();
                try
                {
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

                var session = HandleSessionAndDisposeAsync(pipe, cancellationToken);
                active.Add(session);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await Task.WhenAll(active).ConfigureAwait(false);
        }
    }

    private static async Task DrainCompletedAsync(HashSet<Task> active)
    {
        foreach (var task in active.Where(item => item.IsCompleted).ToArray())
        {
            active.Remove(task);
            await task.ConfigureAwait(false);
        }
    }

    private async Task HandleSessionAndDisposeAsync(
        NamedPipeServerStream pipe,
        CancellationToken serviceToken)
    {
        await using (pipe.ConfigureAwait(false))
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(serviceToken))
        {
            timeout.CancelAfter(SessionTimeout);
            try
            {
                await HandleSessionAsync(pipe, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is
                EndOfStreamException or
                IOException or
                InvalidDataException or
                UnauthorizedAccessException or
                System.Text.Json.JsonException or
                OperationCanceledException)
            {
                _ = exception;
            }
        }
    }

    private async Task HandleSessionAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken)
    {
        var key = await keyProvider.GetKeyAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var challenge = ManagerHandshake.CreateChallenge(key);
            await LengthPrefixedFrame.WriteAsync(
                pipe, ManagerJson.Serialize(challenge), cancellationToken).ConfigureAwait(false);

            var authentication = ManagerJson.Deserialize<ManagerAuthenticationV1>(
                await LengthPrefixedFrame.ReadAsync(pipe, cancellationToken).ConfigureAwait(false));
            ManagerHandshake.ValidateAuthentication(key, challenge, authentication);
            var identity = GetClientIdentity(pipe);

            var request = ManagerJson.Deserialize<ManagerRequestV1>(
                await LengthPrefixedFrame.ReadAsync(pipe, cancellationToken).ConfigureAwait(false));
            var response = await dispatcher.DispatchAsync(
                request, identity, cancellationToken).ConfigureAwait(false);
            await LengthPrefixedFrame.WriteAsync(
                pipe, ManagerJson.Serialize(response), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private ManagerClientIdentity GetClientIdentity(NamedPipeServerStream pipe)
    {
        ManagerClientIdentity? result = null;
        pipe.RunAsClient(() =>
        {
            using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
            var sid = identity.User?.Value ??
                throw new UnauthorizedAccessException("O cliente local não possui SID.");
            var principal = new WindowsPrincipal(identity);
            var isSystem = string.Equals(sid, "S-1-5-18", StringComparison.Ordinal);
            var isAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
            var isApi = string.Equals(
                sid,
                securityOptions.ApiClientSid,
                StringComparison.OrdinalIgnoreCase);
            if (!isSystem && !isAdministrator && !isApi)
            {
                throw new UnauthorizedAccessException(
                    "O token do Windows não possui autorização administrativa.");
            }

            result = new(
                sid,
                isApi
                    ? ServiceDeckManagement.Domain.Manager.ManagerRole.Viewer
                    : ServiceDeckManagement.Domain.Manager.ManagerRole.Administrator,
                isApi);
        });

        return result ??
            throw new UnauthorizedAccessException("Não foi possível identificar o cliente local.");
    }
}
