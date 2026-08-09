using System.Net;
using ServiceDeckManagement.Host.Processes;

namespace ServiceDeckManagement.Host.Health;

internal sealed class HttpHealthProbe(Uri target) : IHealthProbe
{
    private readonly HttpClient client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.None
    });

    public async Task<bool> CheckAsync(
        ManagedProcess process,
        CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, target);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public ValueTask DisposeAsync()
    {
        client.Dispose();
        return ValueTask.CompletedTask;
    }
}
