namespace ServiceDeckManagement.Host.Logging;

/// <summary>
/// Origem de uma entrada de log do processo supervisionado.
/// </summary>
public enum ServiceLogSource
{
    System,
    StandardOutput,
    StandardError
}
