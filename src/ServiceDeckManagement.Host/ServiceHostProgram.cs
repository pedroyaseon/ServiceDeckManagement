using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Host.Health;
using ServiceDeckManagement.Host.Logging;
using ServiceDeckManagement.Host.Processes;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Host;

/// <summary>
/// Ponto de composição do processo de host compartilhado.
/// </summary>
public static class ServiceHostProgram
{
    public static async Task<int> RunAsync(
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("O Service Host requer Windows.");
            return 2;
        }

        if (!ServiceHostArguments.TryParse(arguments, out var parsed, out var error))
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        try
        {
            var rootProvider = new ProductRootLocator(AppContext.BaseDirectory);
            var pathResolver = new PortablePathResolver(rootProvider);
            var validator = new ServiceDefinitionValidator(pathResolver);
            var loader = new ServiceDefinitionLoader(
                rootProvider,
                pathResolver,
                validator);
            var definition = await loader.LoadAsync(
                parsed!.ServiceId,
                cancellationToken).ConfigureAwait(false);

            var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
                new HostApplicationBuilderSettings
                {
                    Args = [],
                    ContentRootPath = rootProvider.RootPath
                });

            builder.Services.AddWindowsService(options =>
                options.ServiceName = $"ServiceDeckManagement.{definition.Definition.Id}");
            builder.Logging.ClearProviders();
            builder.Services.Configure<HostOptions>(options =>
            {
                options.BackgroundServiceExceptionBehavior =
                    BackgroundServiceExceptionBehavior.StopHost;
                options.ShutdownTimeout = TimeSpan.FromSeconds(
                    definition.Definition.StopPolicy.GracefulTimeoutSeconds + 15);
            });

            builder.Services.AddSingleton<IProductRootProvider>(rootProvider);
            builder.Services.AddSingleton<IPortablePathResolver>(pathResolver);
            builder.Services.AddSingleton(definition);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IServiceLogSink, RotatingServiceLogSink>();
            builder.Services.AddSingleton<ProcessStartInfoFactory>();
            builder.Services.AddSingleton<ManagedProcessFactory>();
            builder.Services.AddHostedService<ServiceHostWorker>();

            using var host = builder.Build();
            await host.RunAsync(cancellationToken).ConfigureAwait(false);
            return Environment.ExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ProductRootNotFoundException)
        {
            _ = exception;
            Console.Error.WriteLine(
                "Falha ao iniciar o Service Host. Verifique a raiz, a definição e as permissões locais.");
            return 1;
        }
    }
}
