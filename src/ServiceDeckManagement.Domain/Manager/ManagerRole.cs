namespace ServiceDeckManagement.Domain.Manager;

/// <summary>
/// Papel derivado do token do Windows; nunca é aceito do conteúdo da requisição.
/// </summary>
public enum ManagerRole
{
    Viewer = 0,
    Operator = 1,
    Administrator = 2
}
