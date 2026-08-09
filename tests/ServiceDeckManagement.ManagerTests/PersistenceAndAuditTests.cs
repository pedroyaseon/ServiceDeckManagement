using System.Text;
using ServiceDeckManagement.Application.Manager;
using ServiceDeckManagement.Application.Validation;
using ServiceDeckManagement.Infrastructure.LocalProtocol;
using ServiceDeckManagement.Infrastructure.Manager;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.ManagerTests;

public sealed class PersistenceAndAuditTests
{
    [Fact]
    public async Task Repository_ReplacesDefinitionAtomically()
    {
        using var root = new TestProductRoot();
        var validator = new ServiceDefinitionValidator(new PortablePathResolver(root));
        using var repository = new AtomicServiceDefinitionRepository(root.Paths, validator);
        var first = TestDefinitions.Create(displayName: "Primeiro");
        var second = first with { DisplayName = "Segundo" };

        await repository.SaveAsync(first, CancellationToken.None);
        await repository.SaveAsync(second, CancellationToken.None);
        var loaded = await repository.FindAsync("sample", CancellationToken.None);

        Assert.Equal("Segundo", loaded?.DisplayName);
        Assert.Empty(Directory.EnumerateFiles(
            root.Paths.ServiceDefinitions, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task Repository_SerializesConcurrentWriters()
    {
        using var root = new TestProductRoot();
        var validator = new ServiceDefinitionValidator(new PortablePathResolver(root));
        using var repository = new AtomicServiceDefinitionRepository(root.Paths, validator);

        var writes = Enumerable.Range(0, 20).Select(index =>
            repository.SaveAsync(
                TestDefinitions.Create(displayName: $"Serviço {index}"),
                CancellationToken.None));
        await Task.WhenAll(writes);

        var loaded = await repository.FindAsync("sample", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.StartsWith("Serviço ", loaded.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Audit_DetectsTampering()
    {
        using var root = new TestProductRoot();
        using (var audit = new HashChainedAuditLog(root.Paths))
        {
            await audit.AppendAsync(Event("started"), CancellationToken.None);
            await audit.AppendAsync(Event("stopped"), CancellationToken.None);
            Assert.NotNull(await audit.VerifyAndGetLastHashAsync(CancellationToken.None));
        }

        var content = await File.ReadAllTextAsync(root.Paths.ManagerAudit, Encoding.UTF8);
        await File.WriteAllTextAsync(
            root.Paths.ManagerAudit,
            content.Replace("started", "changed", StringComparison.Ordinal),
            new UTF8Encoding(false));

        using var verifier = new HashChainedAuditLog(root.Paths);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            verifier.VerifyAndGetLastHashAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Dpapi_KeyIsStableAndNotStoredInPlaintext()
    {
        using var root = new TestProductRoot();
        byte[] first;
        using (var provider = new DpapiTransportKeyProvider(root.Paths))
        {
            first = await provider.GetKeyAsync(CancellationToken.None);
        }

        using var secondProvider = new DpapiTransportKeyProvider(root.Paths);
        var second = await secondProvider.GetKeyAsync(CancellationToken.None);
        var stored = await File.ReadAllBytesAsync(root.Paths.ManagerTransportKey);

        Assert.Equal(first, second);
        Assert.NotEqual(first, stored);
    }

    private static AuditEvent Event(string result) => new(
        DateTimeOffset.Parse("2026-08-09T20:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        "S-1-5-18",
        "service.start",
        "sample",
        true,
        result,
        "00000000-0000-0000-0000-000000000001");
}
