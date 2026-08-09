using ServiceDeckManagement.Api;
using ServiceDeckManagement.Application.Abstractions;
using ServiceDeckManagement.Contracts.Api;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.ApiTests;

public sealed class ApiDatabaseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"sdm-api-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Bootstrap_CreatesSingleAdministratorAndSessionCanBeRevoked()
    {
        var database = CreateDatabase();
        await database.InitializeAsync(CancellationToken.None);
        Assert.False(await database.HasUsersAsync(CancellationToken.None));

        var user = await database.CreateAdministratorAsync("pedro.admin", "Senha-Forte-2026!", CancellationToken.None);
        Assert.Equal(ApiRolesV1.Administrator, user.Role);
        Assert.True(await database.HasUsersAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            database.CreateAdministratorAsync("outro", "Outra-Senha-2026!", CancellationToken.None));

        Assert.Null(await database.LoginAsync("pedro.admin", "senha-incorreta", CancellationToken.None));
        var session = await database.LoginAsync("pedro.admin", "Senha-Forte-2026!", CancellationToken.None);
        Assert.NotNull(session);
        var principal = await database.FindSessionAsync(session.AccessToken, CancellationToken.None);
        Assert.Equal(user.Id, principal?.Id);

        await database.RevokeAsync(principal!.SessionId, CancellationToken.None);
        Assert.Null(await database.FindSessionAsync(session.AccessToken, CancellationToken.None));
    }

    [Fact]
    public async Task Credentials_RejectSqlInjectionAndControlCharacters()
    {
        var database = CreateDatabase();
        await database.InitializeAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdministratorAsync("admin' OR 1=1--", "Senha-Forte-2026!", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdministratorAsync("admin", "Senha\nInsegura-2026!", CancellationToken.None));
    }

    [Fact]
    public async Task Audit_UsesBoundedReadsAndPersistsResult()
    {
        var database = CreateDatabase();
        await database.InitializeAsync(CancellationToken.None);
        await database.WriteAuditAsync("actor", "service.start", "sample", true, CancellationToken.None);

        var entries = await database.ReadAuditAsync(10, CancellationToken.None);
        Assert.Single(entries);
        Assert.True(entries[0].Success);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            database.ReadAuditAsync(501, CancellationToken.None));
    }

    [Fact]
    public void BootstrapCode_IsSingleUseAndCaseInsensitive()
    {
        var bootstrap = new BootstrapCode();
        var code = bootstrap.Generate();

        Assert.True(bootstrap.Consume(code.ToLowerInvariant()));
        Assert.False(bootstrap.Consume(code));
        Assert.False(bootstrap.Consume("not-a-valid-code"));
    }

    private ApiDatabase CreateDatabase()
    {
        Directory.CreateDirectory(root);
        return new ApiDatabase(new ProductPaths(new TestRoot(root)));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private sealed record TestRoot(string RootPath) : IProductRootProvider;
}
