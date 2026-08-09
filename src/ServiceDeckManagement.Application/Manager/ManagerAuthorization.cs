using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Domain.Manager;

namespace ServiceDeckManagement.Application.Manager;

public static class ManagerAuthorization
{
    public static bool IsAllowed(ManagerRole role, string operation) => operation switch
    {
        ManagerOperationsV1.Ping or
        ManagerOperationsV1.Inventory or
        ManagerOperationsV1.Logs =>
            role >= ManagerRole.Viewer,
        ManagerOperationsV1.Start or
        ManagerOperationsV1.Stop or
        ManagerOperationsV1.Restart => role >= ManagerRole.Operator,
        ManagerOperationsV1.Create or
        ManagerOperationsV1.Update or
        ManagerOperationsV1.Remove or
        ManagerOperationsV1.Repair => role >= ManagerRole.Administrator,
        _ => false
    };
}
