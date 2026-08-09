namespace ServiceDeckManagement.Domain.Services;

/// <summary>
/// Define como o serviço é iniciado pelo Windows.
/// </summary>
public enum ServiceStartMode
{
    Automatic,
    Manual,
    Disabled
}
