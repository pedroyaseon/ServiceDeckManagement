using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Contracts.Services;
using ServiceDeckManagement.Contracts.Versioning;
using ServiceDeckManagement.Domain.Services;

namespace ServiceDeckManagement.Application.Validation;

/// <summary>
/// Valida o contrato v1 antes que ele atravesse um limite privilegiado.
/// </summary>
public sealed class ServiceDefinitionValidator(
    IPortablePathResolver pathResolver)
{
    private const int MaximumArgumentLength = 4_096;
    private const int MaximumArgumentCount = 128;
    private const int MaximumEnvironmentEntries = 128;
    private const int MaximumEnvironmentValueLength = 32_768;
    private static readonly HashSet<string> ReservedServiceIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "api",
            "host",
            "manager",
            "system"
        };
    private static readonly string[] SensitiveEnvironmentFragments =
    [
        "password",
        "passwd",
        "token",
        "api_key",
        "apikey",
        "client_secret",
        "private_key"
    ];

    public ServiceDefinitionValidationResult Validate(
        ServiceDefinitionV1? definition)
    {
        var errors = new List<ValidationError>();
        if (definition is null)
        {
            errors.Add(new(
                "definition.required",
                "$",
                "A definição do serviço é obrigatória."));
            return new(errors);
        }

        ValidateSchema(definition, errors);
        ValidateIdentity(definition, errors);
        ValidatePaths(definition, errors);
        ValidateArguments(definition, errors);
        ValidateEnvironment(definition, errors);
        ValidateStartMode(definition, errors);
        ValidateRestartPolicy(definition.RestartPolicy, errors);
        ValidateStopPolicy(definition.StopPolicy, errors);
        ValidateLogging(definition.Logging, errors);
        ValidateHealthCheck(definition.HealthCheck, errors);
        return new(errors);
    }

    private static void ValidateSchema(
        ServiceDefinitionV1 definition,
        List<ValidationError> errors)
    {
        if (definition.SchemaVersion != ContractVersions.ServiceDefinitionSchema)
        {
            errors.Add(new(
                "schema.unsupported",
                "schemaVersion",
                $"Apenas o schema {ContractVersions.ServiceDefinitionSchema} é suportado."));
        }
    }

    private static void ValidateIdentity(
        ServiceDefinitionV1 definition,
        List<ValidationError> errors)
    {
        if (!ServiceId.TryCreate(definition.Id, out _))
        {
            errors.Add(new(
                "id.invalid",
                "id",
                "Use de 1 a 63 caracteres: letras minúsculas, números e hífen."));
        }
        else if (ReservedServiceIds.Contains(definition.Id))
        {
            errors.Add(new(
                "id.reserved",
                "id",
                "O identificador é reservado pelo produto."));
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName) ||
            definition.DisplayName.Length > 128 ||
            ContainsControlCharacter(definition.DisplayName))
        {
            errors.Add(new(
                "displayName.invalid",
                "displayName",
                "O nome de exibição deve possuir de 1 a 128 caracteres válidos."));
        }
    }

    private void ValidatePaths(
        ServiceDefinitionV1 definition,
        List<ValidationError> errors)
    {
        ValidatePath(definition.Executable, "executable", errors);
        if (!string.Equals(
                Path.GetExtension(definition.Executable),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new(
                "executable.extension",
                "executable",
                "O executável deve ser um arquivo .exe."));
        }

        ValidatePath(definition.WorkingDirectory, "workingDirectory", errors);
    }

    private void ValidatePath(
        string? value,
        string field,
        List<ValidationError> errors)
    {
        var result = pathResolver.Resolve(value);
        if (!result.IsValid)
        {
            errors.Add(new(
                result.ErrorCode ?? "path.invalid",
                field,
                result.ErrorMessage ?? "O caminho relativo é inválido."));
        }
    }

    private static void ValidateArguments(
        ServiceDefinitionV1 definition,
        List<ValidationError> errors)
    {
        if (definition.Arguments is null ||
            definition.Arguments.Length > MaximumArgumentCount)
        {
            errors.Add(new(
                "arguments.count",
                "arguments",
                $"São permitidos no máximo {MaximumArgumentCount} argumentos."));
            return;
        }

        for (var index = 0; index < definition.Arguments.Length; index++)
        {
            var argument = definition.Arguments[index];
            if (argument is null ||
                argument.Length > MaximumArgumentLength ||
                ContainsControlCharacter(argument))
            {
                errors.Add(new(
                    "arguments.invalid",
                    $"arguments[{index}]",
                    "O argumento contém caractere inválido ou excede o limite."));
            }
        }
    }

    private static void ValidateEnvironment(
        ServiceDefinitionV1 definition,
        List<ValidationError> errors)
    {
        ValidateEnvironmentMap(
            definition.Environment,
            "environment",
            allowSensitiveNames: false,
            errors);
        ValidateEnvironmentMap(
            definition.SecretReferences,
            "secretReferences",
            allowSensitiveNames: true,
            errors);
    }

    private static void ValidateEnvironmentMap(
        Dictionary<string, string>? values,
        string field,
        bool allowSensitiveNames,
        List<ValidationError> errors)
    {
        if (values is null || values.Count > MaximumEnvironmentEntries)
        {
            errors.Add(new(
                $"{field}.count",
                field,
                $"São permitidas no máximo {MaximumEnvironmentEntries} entradas."));
            return;
        }

        var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in values)
        {
            if (!normalizedNames.Add(name))
            {
                errors.Add(new(
                    $"{field}.duplicateName",
                    $"{field}.{name}",
                    "Nomes duplicados sem distinção entre maiúsculas e minúsculas não são permitidos."));
                continue;
            }

            if (!IsValidEnvironmentName(name))
            {
                errors.Add(new(
                    $"{field}.name",
                    $"{field}.{name}",
                    "O nome da variável de ambiente é inválido."));
                continue;
            }

            if (!allowSensitiveNames && IsSensitiveName(name))
            {
                errors.Add(new(
                    "environment.secret",
                    $"environment.{name}",
                    "Valores sensíveis devem usar secretReferences."));
            }

            if (string.IsNullOrEmpty(value) ||
                value.Length > MaximumEnvironmentValueLength ||
                value.Contains('\0'))
            {
                errors.Add(new(
                    $"{field}.value",
                    $"{field}.{name}",
                    "O valor está vazio, contém NUL ou excede o limite."));
            }
        }
    }

    private static void ValidateStartMode(
        ServiceDefinitionV1 definition,
        List<ValidationError> errors)
    {
        if (definition.StartMode is not ("automatic" or "manual" or "disabled"))
        {
            errors.Add(new(
                "startMode.invalid",
                "startMode",
                "Use automatic, manual ou disabled."));
        }
    }

    private static void ValidateRestartPolicy(
        RestartPolicyV1? policy,
        List<ValidationError> errors)
    {
        if (policy is null)
        {
            errors.Add(new(
                "restartPolicy.required",
                "restartPolicy",
                "A política de reinício é obrigatória."));
            return;
        }

        if (policy.MaximumAttempts is < 0 or > 100)
        {
            errors.Add(new(
                "restartPolicy.maximumAttempts",
                "restartPolicy.maximumAttempts",
                "O limite deve estar entre 0 e 100."));
        }

        if (policy.DelaySeconds is < 1 or > 3_600 ||
            policy.MaximumDelaySeconds < policy.DelaySeconds ||
            policy.MaximumDelaySeconds > 86_400)
        {
            errors.Add(new(
                "restartPolicy.delay",
                "restartPolicy",
                "Os atrasos de reinício são inválidos."));
        }

        if (policy.ResetAfterMinutes is < 1 or > 10_080)
        {
            errors.Add(new(
                "restartPolicy.resetAfterMinutes",
                "restartPolicy.resetAfterMinutes",
                "A janela deve estar entre 1 minuto e 7 dias."));
        }
    }

    private static void ValidateStopPolicy(
        StopPolicyV1? policy,
        List<ValidationError> errors)
    {
        if (policy is null || policy.GracefulTimeoutSeconds is < 1 or > 600)
        {
            errors.Add(new(
                "stopPolicy.invalid",
                "stopPolicy.gracefulTimeoutSeconds",
                "O timeout deve estar entre 1 e 600 segundos."));
        }

        if (policy is not null && !policy.TerminateTree)
        {
            errors.Add(new(
                "stopPolicy.terminateTree",
                "stopPolicy.terminateTree",
                "A versão 1 exige o encerramento seguro de toda a árvore de processos."));
        }
    }

    private static void ValidateLogging(
        LoggingPolicyV1? policy,
        List<ValidationError> errors)
    {
        if (policy is null)
        {
            errors.Add(new(
                "logging.required",
                "logging",
                "A política de logs é obrigatória."));
            return;
        }

        if (policy.MaximumFileSizeMb is < 1 or > 1_024 ||
            policy.RetainedFiles is < 1 or > 100 ||
            policy.MaximumTotalSizeMb < policy.MaximumFileSizeMb ||
            policy.MaximumTotalSizeMb > 102_400)
        {
            errors.Add(new(
                "logging.limits",
                "logging",
                "Os limites de tamanho ou retenção são inválidos."));
        }
    }

    private static void ValidateHealthCheck(
        HealthCheckV1? healthCheck,
        List<ValidationError> errors)
    {
        if (healthCheck is null)
        {
            errors.Add(new(
                "healthCheck.required",
                "healthCheck",
                "A configuração de health check é obrigatória."));
            return;
        }

        if (healthCheck.IntervalSeconds is < 1 or > 3_600 ||
            healthCheck.TimeoutSeconds is < 1 ||
            healthCheck.TimeoutSeconds > healthCheck.IntervalSeconds)
        {
            errors.Add(new(
                "healthCheck.interval",
                "healthCheck",
                "O intervalo ou timeout do health check é inválido."));
        }

        switch (healthCheck.Type)
        {
            case "process" when !string.IsNullOrEmpty(healthCheck.Target):
                errors.Add(new(
                    "healthCheck.processTarget",
                    "healthCheck.target",
                    "Health check de processo não aceita target."));
                break;
            case "process":
                break;
            case "http" when !IsLoopbackHttpTarget(healthCheck.Target):
                errors.Add(new(
                    "healthCheck.httpTarget",
                    "healthCheck.target",
                    "O target HTTP deve ser uma URL absoluta em loopback."));
                break;
            case "http":
                break;
            case "tcp" when !IsLoopbackTcpTarget(healthCheck.Target):
                errors.Add(new(
                    "healthCheck.tcpTarget",
                    "healthCheck.target",
                    "O target TCP deve usar loopback e uma porta válida."));
                break;
            case "tcp":
                break;
            default:
                errors.Add(new(
                    "healthCheck.type",
                    "healthCheck.type",
                    "Use process, http ou tcp."));
                break;
        }
    }

    private static bool IsLoopbackHttpTarget(string? target) =>
        Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" &&
        uri.IsLoopback &&
        string.IsNullOrEmpty(uri.UserInfo);

    private static bool IsLoopbackTcpTarget(string? target) =>
        Uri.TryCreate($"tcp://{target}", UriKind.Absolute, out var uri) &&
        uri.IsLoopback &&
        uri.Port is >= 1 and <= 65_535 &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        uri.AbsolutePath == "/";

    private static bool IsValidEnvironmentName(string name)
    {
        if (string.IsNullOrEmpty(name) ||
            name.Length > 128 ||
            name[0] is not ('_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
        {
            return false;
        }

        return name.All(character =>
            character is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9');
    }

    private static bool IsSensitiveName(string name) =>
        SensitiveEnvironmentFragments.Any(fragment =>
            name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsControlCharacter(string value) =>
        value.Any(char.IsControl);
}
