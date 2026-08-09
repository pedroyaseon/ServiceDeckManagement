using ServiceDeckManagement.Domain.Services;

namespace ServiceDeckManagement.Host;

/// <summary>
/// Argumentos estritos aceitos pelo executável compartilhado do Service Host.
/// </summary>
public sealed record ServiceHostArguments(string ServiceId)
{
    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out ServiceHostArguments? result,
        out string? error)
    {
        result = null;
        error = null;

        if (arguments.Count != 2 ||
            !string.Equals(arguments[0], "--service-id", StringComparison.Ordinal))
        {
            error = "Uso: ServiceDeckManagement.Host.exe --service-id <id>";
            return false;
        }

        if (!Domain.Services.ServiceId.TryCreate(arguments[1], out var serviceId))
        {
            error = "O identificador do serviço é inválido.";
            return false;
        }

        result = new(serviceId.Value);
        return true;
    }
}
