using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Console;
using ServiceDeckManagement.Contracts.Api;
using ServiceDeckManagement.Infrastructure.LocalProtocol;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Api;

public static class Program
{
    private static readonly Action<ILogger, Exception?> RequestFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1000, nameof(RequestFailed)),
        "A requisição da API falhou.");

    public static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("A API do Service Deck Management requer Windows.");
            return 2;
        }

        if (args.Length != 0)
        {
            Console.Error.WriteLine("A API não aceita argumentos de linha de comando.");
            return 2;
        }

        try
        {
            var rootProvider = ProductRootLocator.FromApplicationBaseDirectory();
            var paths = new ProductPaths(rootProvider);
            var apiOptions = new ApiConfiguration(paths).Load();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                ContentRootPath = rootProvider.RootPath
            });
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;
                options.Limits.MaxRequestBodySize = 1_048_576;
                options.Listen(IPAddress.Loopback, apiOptions.Port);
            });
            builder.Services.AddWindowsService(options => options.ServiceName = "ServiceDeckManagement.Api");
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(options =>
            {
                options.ColorBehavior = LoggerColorBehavior.Disabled;
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
            });
            Directory.CreateDirectory(paths.ApiProtectionKeys);
            builder.Services.AddDataProtection()
                .SetApplicationName("ServiceDeckManagement.Api.v1")
                .PersistKeysToFileSystem(new DirectoryInfo(paths.ApiProtectionKeys))
                .ProtectKeysWithDpapi(protectToLocalMachine: true);
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.PropertyNameCaseInsensitive = false;
                options.SerializerOptions.AllowTrailingCommas = false;
                options.SerializerOptions.ReadCommentHandling = JsonCommentHandling.Disallow;
                options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                options.SerializerOptions.MaxDepth = 32;
            });
            builder.Services.AddSingleton(rootProvider);
            builder.Services.AddSingleton(paths);
            builder.Services.AddSingleton<ApiDatabase>();
            builder.Services.AddSingleton<BootstrapCode>();
            builder.Services.AddSingleton<ITransportKeyProvider, DpapiTransportKeyReader>();
            builder.Services.AddSingleton<IManagerClient, NamedPipeManagerClient>();
            builder.Services.AddSingleton<ServiceSnapshotState>();
            builder.Services.AddHostedService<ServiceSnapshotWorker>();
            builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 65_536);
            builder.Services.AddProblemDetails();
            builder.Services.AddAuthentication(ApiAuthenticationDefaults.Scheme)
                .AddScheme<AuthenticationSchemeOptions, ApiAuthenticationHandler>(
                    ApiAuthenticationDefaults.Scheme, _ => { });
            builder.Services.AddAuthorizationBuilder()
                .AddPolicy("operator", policy => policy.RequireRole(ApiRolesV1.Operator, ApiRolesV1.Administrator))
                .AddPolicy("administrator", policy => policy.RequireRole(ApiRolesV1.Administrator));
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 240,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
                options.AddFixedWindowLimiter("authentication", limiter =>
                {
                    limiter.PermitLimit = 10;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.AutoReplenishment = true;
                });
            });

            var app = builder.Build();
            app.UseExceptionHandler(handler => handler.Run(async context =>
            {
                var feature = context.Features.Get<IExceptionHandlerFeature>();
                RequestFailed(app.Logger, feature?.Error);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Erro interno").ExecuteAsync(context).ConfigureAwait(false);
            }));
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapServiceDeckApi();
            app.MapHub<ServiceEventsHub>("/api/v1/events").RequireAuthorization();

            var database = app.Services.GetRequiredService<ApiDatabase>();
            await database.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
            if (!await database.HasUsersAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var code = app.Services.GetRequiredService<BootstrapCode>().Generate();
                Console.WriteLine($"Código de inicialização local (válido por 15 minutos): {code}");
            }

            await app.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Falha ao iniciar a API: {exception.Message}");
            return 1;
        }
    }
}
