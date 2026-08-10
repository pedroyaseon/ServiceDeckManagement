using System.Security.Principal;
using ServiceDeckManagement.Setup;

namespace ServiceDeckManagement.SetupTests;

public sealed class ManagerSetupArgumentsTests
{
    [Fact]
    public void Parse_AcceptsOnlyInstallManagerWithDedicatedSid()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ??
            throw new InvalidOperationException("SID ausente.");

        var parsed = ManagerSetupArguments.TryParse(
            ["install-manager", "--launcher-sid", sid], out var request);

        Assert.True(parsed);
        Assert.Equal(sid, request?.LauncherSid);
    }

    [Theory]
    [InlineData()]
    [InlineData("remove-manager")]
    [InlineData("install-manager", "--launcher-sid", "S-1-5-18")]
    [InlineData("install-manager", "--launcher-sid", "S-1-1-0")]
    [InlineData("install-manager", "--launcher-sid", "S-1-5-11")]
    [InlineData("install-manager", "--launcher-sid", "S-1-5-32-545")]
    [InlineData("install-manager", "--unknown", "S-1-5-21-1")]
    [InlineData("install-manager", "--launcher-sid", "S-1-5-21-1", "extra")]
    public void Parse_RejectsUnknownOrPrivilegedInput(params string[] arguments)
    {
        Assert.False(ManagerSetupArguments.TryParse(arguments, out var request));
        Assert.Null(request);
    }
}
