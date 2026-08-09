using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using ServiceDeckManagement.Contracts.Api;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Infrastructure.LocalProtocol;

namespace ServiceDeckManagement.Api;

public static class ApiEndpoints
{
    private const string SystemActor = "00000000-0000-0000-0000-000000000001";

    public static void MapServiceDeckApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/system/version", () => new VersionResponseV1 { Version = "1.0.0-beta.1" }).AllowAnonymous();
        api.MapGet("/system/health", HealthAsync).AllowAnonymous();
        api.MapGet("/bootstrap/status", BootstrapStatusAsync).AllowAnonymous();
        api.MapPost("/bootstrap", BootstrapAsync).AllowAnonymous().RequireRateLimiting("authentication");
        api.MapPost("/sessions", LoginAsync).AllowAnonymous().RequireRateLimiting("authentication");
        api.MapDelete("/sessions/current", LogoutAsync).RequireAuthorization();

        api.MapGet("/services", ListServicesAsync).RequireAuthorization();
        api.MapGet("/services/snapshot", (ServiceSnapshotState state) => Results.Ok(state.Current)).RequireAuthorization();
        api.MapGet("/services/{serviceId}", GetServiceAsync).RequireAuthorization();
        api.MapGet("/services/{serviceId}/logs", ReadLogsAsync).RequireAuthorization();
        api.MapPost("/services", CreateServiceAsync).RequireAuthorization("administrator");
        api.MapPut("/services/{serviceId}", UpdateServiceAsync).RequireAuthorization("administrator");
        api.MapDelete("/services/{serviceId}", RemoveServiceAsync).RequireAuthorization("administrator");
        api.MapPost("/services/{serviceId}/start", StartServiceAsync).RequireAuthorization("operator");
        api.MapPost("/services/{serviceId}/stop", StopServiceAsync).RequireAuthorization("operator");
        api.MapPost("/services/{serviceId}/restart", RestartServiceAsync).RequireAuthorization("operator");
        api.MapPost("/services/{serviceId}/repair", RepairServiceAsync).RequireAuthorization("administrator");
        api.MapGet("/audit", ReadAuditAsync).RequireAuthorization("administrator");
    }

    private static async Task<IResult> HealthAsync(IManagerClient manager, CancellationToken cancellationToken)
    {
        try
        {
            var response = await manager.SendAsync(
                ManagerOperationsV1.Ping, new { }, SystemActor, ApiRolesV1.Viewer, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new SystemHealthV1 { Api = "online", Manager = response.Success ? "online" : "degraded" });
        }
        catch (Exception exception) when (IsManagerUnavailable(exception))
        {
            return Results.Ok(new SystemHealthV1 { Api = "online", Manager = "offline" });
        }
    }

    private static async Task<IResult> BootstrapStatusAsync(ApiDatabase database, CancellationToken cancellationToken) =>
        Results.Ok(new BootstrapStatusV1 { Required = !await database.HasUsersAsync(cancellationToken).ConfigureAwait(false) });

    private static async Task<IResult> BootstrapAsync(
        HttpContext context,
        BootstrapRequestV1 request,
        ApiDatabase database,
        BootstrapCode bootstrap,
        CancellationToken cancellationToken)
    {
        if (context.Connection.RemoteIpAddress is null || !System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
        {
            return Results.NotFound();
        }

        if (await database.HasUsersAsync(cancellationToken).ConfigureAwait(false))
        {
            return Results.Conflict(new { message = "A inicialização já foi concluída." });
        }

        if (!ApiDatabase.AreValidCredentials(request.Username, request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Usuário ou senha inválidos."] });
        }

        if (!bootstrap.Consume(request.Code))
        {
            return Results.Unauthorized();
        }

        try
        {
            var user = await database.CreateAdministratorAsync(request.Username, request.Password, cancellationToken).ConfigureAwait(false);
            await database.WriteAuditAsync(user.Id, "bootstrap.complete", user.Id, true, cancellationToken).ConfigureAwait(false);
            return Results.Created("/api/v1/sessions", user);
        }
        catch (ArgumentException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Usuário ou senha inválidos."] });
        }
    }

    private static async Task<IResult> LoginAsync(LoginRequestV1 request, ApiDatabase database, CancellationToken cancellationToken)
    {
        var session = await database.LoginAsync(request.Username, request.Password, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            await database.WriteAuditAsync("anonymous", "session.login", "user", false, cancellationToken).ConfigureAwait(false);
            return Results.Unauthorized();
        }

        await database.WriteAuditAsync(session.User.Id, "session.login", session.User.Id, true, cancellationToken).ConfigureAwait(false);
        return Results.Ok(session);
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, ApiDatabase database, CancellationToken cancellationToken)
    {
        var sessionId = context.User.FindFirstValue("session_id");
        if (sessionId is not null)
        {
            await database.RevokeAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ListServicesAsync(HttpContext context, IManagerClient manager, CancellationToken cancellationToken) =>
        await SendManagerAsync(context, manager, ManagerOperationsV1.Inventory, new { }, cancellationToken).ConfigureAwait(false);

    private static async Task<IResult> GetServiceAsync(string serviceId, HttpContext context, IManagerClient manager, CancellationToken cancellationToken)
        => await SendManagerAsync(
            context,
            manager,
            ManagerOperationsV1.Details,
            new ServiceIdPayloadV1 { ServiceId = serviceId },
            cancellationToken).ConfigureAwait(false);

    private static Task<IResult> ReadLogsAsync(string serviceId, long after, int limit, HttpContext context, IManagerClient manager, CancellationToken cancellationToken) =>
        SendManagerAsync(context, manager, ManagerOperationsV1.Logs,
            new ServiceLogsPayloadV1 { ServiceId = serviceId, AfterSequence = after, Limit = limit is 0 ? 200 : limit }, cancellationToken);

    private static Task<IResult> CreateServiceAsync(ServiceDefinitionV1 definition, HttpContext context, IManagerClient manager, ApiDatabase database, CancellationToken cancellationToken) =>
        MutateAsync(context, manager, database, ManagerOperationsV1.Create, definition, definition.Id, cancellationToken);

    private static Task<IResult> UpdateServiceAsync(string serviceId, ServiceDefinitionV1 definition, HttpContext context, IManagerClient manager, ApiDatabase database, CancellationToken cancellationToken) =>
        !string.Equals(serviceId, definition.Id, StringComparison.Ordinal)
            ? Task.FromResult<IResult>(Results.BadRequest(new { message = "O identificador da rota não corresponde ao contrato." }))
            : MutateAsync(context, manager, database, ManagerOperationsV1.Update, definition, serviceId, cancellationToken);

    private static Task<IResult> RemoveServiceAsync(string serviceId, HttpContext context, IManagerClient manager, ApiDatabase database, CancellationToken cancellationToken) =>
        MutateAsync(context, manager, database, ManagerOperationsV1.Remove, new ServiceIdPayloadV1 { ServiceId = serviceId }, serviceId, cancellationToken);

    private static Task<IResult> StartServiceAsync(string serviceId, HttpContext context, IManagerClient manager, ApiDatabase database, CancellationToken cancellationToken) =>
        MutateAsync(context, manager, database, ManagerOperationsV1.Start, new ServiceIdPayloadV1 { ServiceId = serviceId }, serviceId, cancellationToken);

    private static Task<IResult> StopServiceAsync(string serviceId, HttpContext context, IManagerClient manager, ApiDatabase database, CancellationToken cancellationToken) =>
        MutateAsync(context, manager, database, ManagerOperationsV1.Stop, new ServiceIdPayloadV1 { ServiceId = serviceId }, serviceId, cancellationToken);

    private static Task<IResult> RestartServiceAsync(string serviceId, HttpContext context, IManagerClient manager, ApiDatabase database, CancellationToken cancellationToken) =>
        MutateAsync(context, manager, database, ManagerOperationsV1.Restart, new ServiceIdPayloadV1 { ServiceId = serviceId }, serviceId, cancellationToken);

    private static Task<IResult> RepairServiceAsync(string serviceId, HttpContext context, IManagerClient manager, ApiDatabase database, CancellationToken cancellationToken) =>
        MutateAsync(context, manager, database, ManagerOperationsV1.Repair, new ServiceIdPayloadV1 { ServiceId = serviceId }, serviceId, cancellationToken);

    private static async Task<IResult> ReadAuditAsync(int limit, ApiDatabase database, CancellationToken cancellationToken)
    {
        try { return Results.Ok(await database.ReadAuditAsync(limit is 0 ? 100 : limit, cancellationToken).ConfigureAwait(false)); }
        catch (ArgumentOutOfRangeException) { return Results.BadRequest(new { message = "O limite deve estar entre 1 e 500." }); }
    }

    private static async Task<IResult> MutateAsync(HttpContext context, IManagerClient manager, ApiDatabase database, string operation, object payload, string target, CancellationToken cancellationToken)
    {
        var actor = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var response = await TryManagerAsync(context, manager, operation, payload, cancellationToken).ConfigureAwait(false);
        var success = response.Response?.Success == true;
        await database.WriteAuditAsync(actor, operation, target, success, cancellationToken).ConfigureAwait(false);
        return response.Result ?? (success ? Results.NoContent() : Results.Conflict(new { message = "A operação não pôde ser concluída." }));
    }

    private static async Task<IResult> SendManagerAsync(HttpContext context, IManagerClient manager, string operation, object payload, CancellationToken cancellationToken)
    {
        var response = await TryManagerAsync(context, manager, operation, payload, cancellationToken).ConfigureAwait(false);
        return response.Result ?? (response.Response!.Success
            ? Results.Json(response.Response.Data)
            : Results.Conflict(new { message = "A operação não pôde ser concluída." }));
    }

    private static async Task<(ManagerResponseV1? Response, IResult? Result)> TryManagerAsync(HttpContext context, IManagerClient manager, string operation, object payload, CancellationToken cancellationToken)
    {
        try
        {
            var actor = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var role = context.User.FindFirstValue(ClaimTypes.Role)!;
            return (await manager.SendAsync(operation, payload, actor, role, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception) when (IsManagerUnavailable(exception))
        {
            return (null, Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Manager indisponível"));
        }
    }

    private static bool IsManagerUnavailable(Exception exception) => exception is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException;
}
