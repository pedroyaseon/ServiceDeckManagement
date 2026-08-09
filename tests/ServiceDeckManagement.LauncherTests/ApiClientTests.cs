using System.Net;
using System.Text;
using ServiceDeckManagement.Launcher;

namespace ServiceDeckManagement.LauncherTests;

public sealed class ApiClientTests
{
    [Fact]
    public async Task Login_StoresTokenOnlyInMemory_AndSendsBearerOnAuthenticatedCalls()
    {
        string? authorization = null;
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v1/sessions")
            {
                return Json(HttpStatusCode.OK,
                    """{"accessToken":"memory-token","expiresAt":"2030-01-01T00:00:00Z","user":{"id":"1","username":"pedro","role":"administrator"}}""");
            }

            authorization = request.Headers.Authorization?.ToString();
            return Json(HttpStatusCode.OK, "[]");
        });
        using var client = new ServiceDeckApiClient(new(new Uri("http://127.0.0.1:5180/")), handler);

        await client.LoginAsync("pedro", "senha-segura", CancellationToken.None);
        await client.GetServicesAsync(CancellationToken.None);

        Assert.Equal("memory-token", client.AccessToken);
        Assert.Equal("Bearer memory-token", authorization);
        Assert.Equal("administrator", client.CurrentUser?.Role);
    }

    [Fact]
    public async Task ManagerUnavailable_IsMappedToStableLocalMessage()
    {
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v1/sessions")
            {
                return Json(HttpStatusCode.OK,
                    """{"accessToken":"token","expiresAt":"2030-01-01T00:00:00Z","user":{"id":"1","username":"operator","role":"operator"}}""");
            }

            return Json(HttpStatusCode.ServiceUnavailable, "{}");
        });
        using var client = new ServiceDeckApiClient(new(new Uri("http://127.0.0.1:5180/")), handler);
        await client.LoginAsync("operator", "senha-segura", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetServicesAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal("O Manager está indisponível.", exception.Message);
    }

    [Fact]
    public async Task AuthenticatedCall_WithoutSession_IsRejectedBeforeNetwork()
    {
        var requests = 0;
        using var client = new ServiceDeckApiClient(
            new(new Uri("http://127.0.0.1:5180/")),
            new StubHandler(_ =>
            {
                requests++;
                return Json(HttpStatusCode.OK, "[]");
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetServicesAsync(CancellationToken.None));

        Assert.Equal(0, requests);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responder(request));
        }
    }
}
