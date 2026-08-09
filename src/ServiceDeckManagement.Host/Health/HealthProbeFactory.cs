using ServiceDeckManagement.Contracts.Services;

namespace ServiceDeckManagement.Host.Health;

/// <summary>
/// Cria somente probes previamente validados pelo contrato v1.
/// </summary>
public static class HealthProbeFactory
{
    public static IHealthProbe Create(HealthCheckV1 healthCheck) => healthCheck.Type switch
    {
        "process" => new ProcessHealthProbe(),
        "http" => new HttpHealthProbe(new Uri(
            healthCheck.Target!,
            UriKind.Absolute)),
        "tcp" => CreateTcpProbe(healthCheck.Target!),
        _ => throw new InvalidDataException("O tipo de health check não é suportado.")
    };

    private static TcpHealthProbe CreateTcpProbe(string target)
    {
        var uri = new Uri($"tcp://{target}", UriKind.Absolute);
        return new(uri.Host, uri.Port);
    }
}
