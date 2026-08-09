namespace ServiceDeckManagement.Application.Validation;

/// <summary>
/// Resultado completo, sem interrupção no primeiro campo inválido.
/// </summary>
public sealed class ServiceDefinitionValidationResult(
    IReadOnlyCollection<ValidationError> errors)
{
    public IReadOnlyCollection<ValidationError> Errors { get; } = errors;

    public bool IsValid => Errors.Count == 0;
}
