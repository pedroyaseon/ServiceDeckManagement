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
        Assert.Null(options.LauncherClientSid);
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
            {"schemaVersion":1,"apiClientSid":null,"launcherClientSid":"{{sid}}"}
            """);

        var options = new ManagerSecurityConfigurationLoader(root.Paths).Load();

        Assert.Null(options.ApiClientSid);
        Assert.Equal(sid, options.LauncherClientSid);
    }

    [Fact]
    public void Configuration_RejectsPrivilegedOrUnknownFields()
    {
        using var root = new TestProductRoot();
        Directory.CreateDirectory(root.Paths.Configuration);
        var path = Path.Combine(root.Paths.Configuration, "manager-security.json");
        File.WriteAllText(path, "{\"schemaVersion\":1,\"apiClientSid\":\"S-1-5-18\",\"launcherClientSid\":null}");
        Assert.Throws<InvalidDataException>(() => new ManagerSecurityConfigurationLoader(root.Paths).Load());

        File.WriteAllText(path, "{\"schemaVersion\":1,\"apiClientSid\":null,\"launcherClientSid\":null,\"unknown\":true}");
        Assert.Throws<InvalidDataException>(() => new ManagerSecurityConfigurationLoader(root.Paths).Load());
    }

    [Fact]
    public void Configuration_RejectsSharedApiAndLauncherIdentity()
    {
        using var root = new TestProductRoot();
        Directory.CreateDirectory(root.Paths.Configuration);
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("SID ausente.");
        File.WriteAllText(
            Path.Combine(root.Paths.Configuration, "manager-security.json"),
            $$"""
            {"schemaVersion":1,"apiClientSid":"{{sid}}","launcherClientSid":"{{sid}}"}
            """);

        Assert.Throws<InvalidDataException>(() => new ManagerSecurityConfigurationLoader(root.Paths).Load());
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"apiClientSid\":null}")]
    [InlineData("{\"schemaVersion\":1,\"launcherClientSid\":null}")]
    public void Configuration_RejectsMissingClientFields(string json)
    {
        using var root = new TestProductRoot();
        Directory.CreateDirectory(root.Paths.Configuration);
        File.WriteAllText(
            Path.Combine(root.Paths.Configuration, "manager-security.json"),
            json);

        Assert.Throws<InvalidDataException>(() =>
            new ManagerSecurityConfigurationLoader(root.Paths).Load());
    }
}
