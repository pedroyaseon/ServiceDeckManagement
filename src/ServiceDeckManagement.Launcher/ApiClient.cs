using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using ServiceDeckManagement.Contracts.Api;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.Launcher;

public sealed class ApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class ServiceDeckApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    private readonly HttpClient httpClient;
    private bool disposed;

    public ServiceDeckApiClient(LauncherOptions options, HttpMessageHandler? handler = null)
    {
        httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        httpClient.BaseAddress = options.ApiBaseUri;
        httpClient.Timeout = TimeSpan.FromSeconds(8);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ServiceDeckManagement-Launcher/1.0");
    }

    public string? AccessToken { get; private set; }

    public UserSummaryV1? CurrentUser { get; private set; }

    public Uri BaseAddress => httpClient.BaseAddress!;

    public async Task<SystemHealthV1> GetHealthAsync(CancellationToken cancellationToken) =>
        await GetAsync<SystemHealthV1>("api/v1/system/health", authenticated: false, cancellationToken).ConfigureAwait(false);

    public async Task<BootstrapStatusV1> GetBootstrapStatusAsync(CancellationToken cancellationToken) =>
        await GetAsync<BootstrapStatusV1>("api/v1/bootstrap/status", authenticated: false, cancellationToken).ConfigureAwait(false);

    public async Task BootstrapAsync(
        string code,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/v1/bootstrap", authenticated: false);
        request.Content = JsonContent.Create(
            new BootstrapRequestV1 { Code = code, Username = username, Password = password },
            options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionResponseV1> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/v1/sessions", authenticated: false);
        request.Content = JsonContent.Create(new LoginRequestV1 { Username = username, Password = password }, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var session = await response.Content.ReadFromJsonAsync<SessionResponseV1>(JsonOptions, cancellationToken).ConfigureAwait(false) ??
            throw new InvalidDataException("A API retornou uma sessão vazia.");
        AccessToken = session.AccessToken;
        CurrentUser = session.User;
        return session;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        if (AccessToken is null) return;
        using var request = CreateRequest(HttpMethod.Delete, "api/v1/sessions/current", authenticated: true);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        AccessToken = null;
        CurrentUser = null;
    }

    public async Task<IReadOnlyList<ManagedServiceSnapshotV1>> GetServicesAsync(CancellationToken cancellationToken) =>
        await GetAsync<IReadOnlyList<ManagedServiceSnapshotV1>>("api/v1/services", authenticated: true, cancellationToken).ConfigureAwait(false);

    public async Task<ManagedServiceDetailsV1> GetServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        await GetAsync<ManagedServiceDetailsV1>($"api/v1/services/{Escape(serviceId)}", authenticated: true, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ServiceLogEntryV1>> GetLogsAsync(
        string serviceId,
        long afterSequence,
        int limit,
        CancellationToken cancellationToken) =>
        await GetAsync<IReadOnlyList<ServiceLogEntryV1>>(
            $"api/v1/services/{Escape(serviceId)}/logs?after={afterSequence}&limit={limit}",
            authenticated: true,
            cancellationToken).ConfigureAwait(false);

    public Task CreateServiceAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
        SendJsonAsync(HttpMethod.Post, "api/v1/services", definition, cancellationToken);

    public Task UpdateServiceAsync(ServiceDefinitionV1 definition, CancellationToken cancellationToken) =>
        SendJsonAsync(HttpMethod.Put, $"api/v1/services/{Escape(definition.Id)}", definition, cancellationToken);

    public Task RemoveServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Delete, $"api/v1/services/{Escape(serviceId)}", cancellationToken);

    public Task StartServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Post, $"api/v1/services/{Escape(serviceId)}/start", cancellationToken);

    public Task StopServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Post, $"api/v1/services/{Escape(serviceId)}/stop", cancellationToken);

    public Task RestartServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Post, $"api/v1/services/{Escape(serviceId)}/restart", cancellationToken);

    public Task RepairServiceAsync(string serviceId, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Post, $"api/v1/services/{Escape(serviceId)}/repair", cancellationToken);

    private async Task<T> GetAsync<T>(string path, bool authenticated, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path, authenticated);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false) ??
            throw new InvalidDataException("A resposta da API está vazia.");
    }

    private async Task SendJsonAsync(HttpMethod method, string path, object body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, authenticated: true);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendWithoutBodyAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, authenticated: true);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool authenticated)
    {
        var request = new HttpRequestMessage(method, path);
        if (authenticated)
        {
            if (AccessToken is null)
            {
                request.Dispose();
                throw new InvalidOperationException("Não existe uma sessão autenticada.");
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        string message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sessão inválida ou credenciais incorretas.",
            HttpStatusCode.Forbidden => "Sua função não permite esta operação.",
            HttpStatusCode.NotFound => "O recurso solicitado não foi encontrado.",
            HttpStatusCode.Conflict => "A operação entrou em conflito com o estado atual.",
            HttpStatusCode.ServiceUnavailable => "O Manager está indisponível.",
            HttpStatusCode.TooManyRequests => "Muitas tentativas. Aguarde e tente novamente.",
            _ => "A API não pôde concluir a operação."
        };
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(problem?.Title) &&
                problem.Title.Length <= 160 &&
                !problem.Title.Any(char.IsControl))
            {
                message = problem.Title;
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            _ = exception;
        }
        throw new ApiException(response.StatusCode, message);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        httpClient.Dispose();
    }

    private sealed record ApiProblem(string? Title);
}
