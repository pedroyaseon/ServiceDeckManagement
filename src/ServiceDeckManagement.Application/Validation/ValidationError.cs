namespace ServiceDeckManagement.Application.Validation;

/// <summary>
/// Erro estável de validação de configuração.
/// </summary>
public sealed record ValidationError(
    string Code,
    string Field,
    string Message);
