using System.Security.Principal;
using ServiceDeckManagement.Infrastructure.Security;

namespace ServiceDeckManagement.ManagerTests;

public sealed class ManagerSecurityConfigurationTests
{
    [Fact]
    public void MissingConfiguration_DefaultsToAdministratorsOnly()
    {
        using var root = new TestProductRoot();

        var options = new ManagerSecurityConfigurationLoader(root.Paths).Load();

        Assert.Null(options.ApiClientSid);
    }

    [Fact]
    public void Configuration_AcceptsDedicatedWindowsSid()
    {
        using var root = new TestProductRoot();
        Directory.CreateDirectory(root.Paths.Configuration);
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("SID ausente.");
        File.WriteAllText(
            Path.Combine(root.Paths.Configuration, "manager-security.json"),
            $$"""
            {"schemaVersion":1,"apiClientSid":"{{sid}}"}
            """);

        var options = new ManagerSecurityConfigurationLoader(root.Paths).Load();

        Assert.Equal(sid, options.ApiClientSid);
    }

    [Fact]
    public void Configuration_RejectsPrivilegedOrUnknownFields()
    {
        using var root = new TestProductRoot();
        Directory.CreateDirectory(root.Paths.Configuration);
        var path = Path.Combine(root.Paths.Configuration, "manager-security.json");
        File.WriteAllText(path, "{\"schemaVersion\":1,\"apiClientSid\":\"S-1-5-18\"}");
        Assert.Throws<InvalidDataException>(() => new ManagerSecurityConfigurationLoader(root.Paths).Load());

        File.WriteAllText(path, "{\"schemaVersion\":1,\"apiClientSid\":null,\"unknown\":true}");
        Assert.Throws<InvalidDataException>(() => new ManagerSecurityConfigurationLoader(root.Paths).Load());
    }
}
