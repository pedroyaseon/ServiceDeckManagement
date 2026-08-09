namespace ServiceDeckManagement.Domain.Services;

/// <summary>
/// Tipos de verificação de disponibilidade aceitos na versão 1.
/// </summary>
public enum HealthCheckKind
{
    Process,
    Http,
    Tcp
}
