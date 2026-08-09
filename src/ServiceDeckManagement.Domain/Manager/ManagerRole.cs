namespace ServiceDeckManagement.Domain.Manager;

/// <summary>
/// Papel efetivo validado pelo Manager. Clientes diretos usam o token do
/// Windows; delegação só é aceita do SID configurado para a API.
/// </summary>
public enum ManagerRole
{
    Viewer = 0,
    Operator = 1,
    Administrator = 2
}
