using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Infrastructure.LocalProtocol;
using ServiceDeckManagement.Infrastructure.Manager;
using ServiceDeckManagement.Infrastructure.Paths;
using ServiceDeckManagement.Infrastructure.Security;
using ServiceDeckManagement.Infrastructure.WindowsServices;

namespace ServiceDeckManagement.Manager;

public static class ManagerProgram
{
    public static async Task<int> RunAsync(
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("O Service Manager requer Windows.");
            return 2;
        }

        if (arguments.Length != 0)
        {
            Console.Error.WriteLine("O Service Manager não aceita argumentos.");
            return 2;
        }

        try
        {
            var rootProvider = new ProductRootLocator(AppContext.BaseDirectory);
            var paths = new ProductPaths(rootProvider);
            var resolver = new PortablePathResolver(rootProvider);
            var validator = new ServiceDefinitionValidator(resolver);
            var securityOptions = new ManagerSecurityConfigurationLoader(paths).Load();
            var aclHardener = new PortableAclHardener(paths);
            aclHardener.Apply(securityOptions);
            var transportKeyProvider = new DpapiTransportKeyProvider(paths);
            var transportKey = await transportKeyProvider.GetKeyAsync(cancellationToken)
                .ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(transportKey);
            aclHardener.ProtectTransportKey(securityOptions);
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = [],
                ContentRootPath = rootProvider.RootPath
            });
            builder.Services.AddWindowsService(options =>
                options.ServiceName = "ServiceDeckManagement.Manager");
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IProductRootProvider>(rootProvider);
            builder.Services.AddSingleton<IPortablePathResolver>(resolver);
            builder.Services.AddSingleton(paths);
            builder.Services.AddSingleton(validator);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IServiceDefinitionRepository,
                AtomicServiceDefinitionRepository>();
            builder.Services.AddSingleton<IAuditLog, HashChainedAuditLog>();
            builder.Services.AddSingleton(securityOptions);
            builder.Services.AddSingleton<ITransportKeyProvider>(transportKeyProvider);
            builder.Services.AddSingleton<IWindowsServiceControlBackend,
                NativeWindowsServiceControlBackend>();
            builder.Services.AddSingleton<IManagedServiceController,
                WindowsScmServiceController>();
            builder.Services.AddSingleton<ServiceManagerCoordinator>();
            builder.Services.AddSingleton<IServiceLogReader, ServiceLogReader>();
            builder.Services.AddSingleton<ManagerRequestDispatcher>();
            builder.Services.AddSingleton<ManagerPipeFactory>();
            builder.Services.AddSingleton<ManagerPipeServer>();
            builder.Services.AddHostedService<ManagerWorker>();

            using var host = builder.Build();
            await host.RunAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ProductRootNotFoundException or
            System.Security.Cryptography.CryptographicException)
        {
            _ = exception;
            Console.Error.WriteLine(
                "Falha ao iniciar o Service Manager. Verifique a raiz e as permissões locais.");
            return 1;
        }
    }
}
