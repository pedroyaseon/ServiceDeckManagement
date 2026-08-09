using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using ServiceDeckManagement.Infrastructure.Security;
using ServiceDeckManagement.Manager;

namespace ServiceDeckManagement.ManagerTests;

public sealed class ManagerPipeFactoryTests
{
    [Fact]
    public void PipeAcl_GrantsOnlyTransportRightsToExplicitLauncherSid()
    {
        var launcherSid = WindowsIdentity.GetCurrent().User ??
            throw new InvalidOperationException("SID do usuário atual ausente.");
        var factory = new ManagerPipeFactory(
            new ManagerSecurityOptions(ApiClientSid: null, launcherSid.Value));

        using var pipe = factory.Create();
        var rules = pipe.GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>();

        var rule = Assert.Single(rules, candidate =>
            launcherSid.Equals(candidate.IdentityReference));
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.True(rule.PipeAccessRights.HasFlag(PipeAccessRights.ReadWrite));
        Assert.True(rule.PipeAccessRights.HasFlag(PipeAccessRights.Synchronize));
        Assert.False(rule.PipeAccessRights.HasFlag(PipeAccessRights.ChangePermissions));
        Assert.False(rule.PipeAccessRights.HasFlag(PipeAccessRights.TakeOwnership));
    }
}
