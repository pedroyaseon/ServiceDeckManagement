using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using ServiceDeckManagement.Contracts.Api;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Api;

public sealed record ApiPrincipal(string Id, string Username, string Role, string SessionId);

public sealed class ApiDatabase(ProductPaths paths)
{
    private static readonly ApiUser DummyUser = new(string.Empty, string.Empty, ApiRolesV1.Viewer);
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<ApiUser>().HashPassword(DummyUser, "dummy-password-never-used"));
    private readonly PasswordHasher<ApiUser> passwordHasher = new();

    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = paths.ApiDatabase,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        Pooling = false
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.ApiData);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                username TEXT NOT NULL,
                normalized_username TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                role TEXT NOT NULL CHECK (role IN ('viewer', 'operator', 'administrator')),
                active INTEGER NOT NULL CHECK (active IN (0, 1)),
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                token_hash TEXT NOT NULL UNIQUE,
                expires_at TEXT NOT NULL,
                revoked_at TEXT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS audit (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                actor_id TEXT NOT NULL,
                action TEXT NOT NULL,
                target TEXT NOT NULL,
                success INTEGER NOT NULL CHECK (success IN (0, 1))
            );
            CREATE INDEX IF NOT EXISTS ix_sessions_token_hash ON sessions(token_hash);
            CREATE INDEX IF NOT EXISTS ix_audit_timestamp ON audit(timestamp DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM users LIMIT 1);";
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    public async Task<UserSummaryV1> CreateAdministratorAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        ValidateCredentials(username, password);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var count = connection.CreateCommand();
        count.Transaction = (SqliteTransaction)transaction;
        count.CommandText = "SELECT COUNT(*) FROM users;";
        if (Convert.ToInt64(
                await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                System.Globalization.CultureInfo.InvariantCulture) != 0)
        {
            throw new InvalidOperationException("A inicialização administrativa já foi concluída.");
        }

        var user = new ApiUser(Guid.NewGuid().ToString("D"), username.Trim(), ApiRolesV1.Administrator);
        await using var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = """
            INSERT INTO users (id, username, normalized_username, password_hash, role, active, created_at)
            VALUES ($id, $username, $normalized, $passwordHash, $role, 1, $createdAt);
            """;
        insert.Parameters.AddWithValue("$id", user.Id);
        insert.Parameters.AddWithValue("$username", user.Username);
        insert.Parameters.AddWithValue("$normalized", NormalizeUsername(user.Username));
        insert.Parameters.AddWithValue("$passwordHash", passwordHasher.HashPassword(user, password));
        insert.Parameters.AddWithValue("$role", user.Role);
        insert.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new() { Id = user.Id, Username = user.Username, Role = user.Role };
    }

    public async Task<SessionResponseV1?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (username.Length is < 1 or > 64 || password.Length is < 1 or > 128)
        {
            return null;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var query = connection.CreateCommand();
        query.CommandText = """
            SELECT id, username, password_hash, role
            FROM users
            WHERE normalized_username = $normalized AND active = 1;
            """;
        query.Parameters.AddWithValue("$normalized", NormalizeUsername(username));
        await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            passwordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash.Value, password);
            return null;
        }

        var user = new ApiUser(reader.GetString(0), reader.GetString(1), reader.GetString(3));
        if (passwordHasher.VerifyHashedPassword(user, reader.GetString(2), password) == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
        var sessionId = Guid.NewGuid().ToString("D");
        await reader.DisposeAsync().ConfigureAwait(false);
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO sessions (id, user_id, token_hash, expires_at, revoked_at, created_at)
            VALUES ($id, $userId, $tokenHash, $expiresAt, NULL, $createdAt);
            """;
        insert.Parameters.AddWithValue("$id", sessionId);
        insert.Parameters.AddWithValue("$userId", user.Id);
        insert.Parameters.AddWithValue("$tokenHash", HashToken(token));
        insert.Parameters.AddWithValue("$expiresAt", expiresAt.ToString("O"));
        insert.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return new()
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            User = new() { Id = user.Id, Username = user.Username, Role = user.Role }
        };
    }

    public async Task<ApiPrincipal?> FindSessionAsync(string token, CancellationToken cancellationToken)
    {
        if (token.Length is < 40 or > 128)
        {
            return null;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.id, u.username, u.role, s.id, s.expires_at
            FROM sessions s JOIN users u ON u.id = s.user_id
            WHERE s.token_hash = $tokenHash AND s.revoked_at IS NULL AND u.active = 1;
            """;
        command.Parameters.AddWithValue("$tokenHash", HashToken(token));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture) <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }

    public async Task RevokeAsync(string sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE sessions SET revoked_at = $now WHERE id = $id AND revoked_at IS NULL;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAuditAsync(string actorId, string action, string target, bool success, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit (timestamp, actor_id, action, target, success)
            VALUES ($timestamp, $actorId, $action, $target, $success);
            """;
        command.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$actorId", actorId);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$target", target);
        command.Parameters.AddWithValue("$success", success ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditEntryV1>> ReadAuditAsync(int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var result = new List<AuditEntryV1>();
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, timestamp, actor_id, action, target, success
            FROM audit ORDER BY id DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new()
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
                ActorId = reader.GetString(2),
                Action = reader.GetString(3),
                Target = reader.GetString(4),
                Success = reader.GetInt64(5) == 1
            });
        }

        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool AreValidCredentials(string username, string password)
    {
        var trimmed = username.Trim();
        if (trimmed.Length is < 3 or > 64 ||
            trimmed.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            return false;
        }

        return password.Length is >= 12 and <= 128 && !password.Any(char.IsControl);
    }

    private static void ValidateCredentials(string username, string password)
    {
        if (!AreValidCredentials(username, password))
        {
            throw new ArgumentException("O usuário ou a senha não atende aos requisitos mínimos.", nameof(username));
        }
    }

    private sealed record ApiUser(string Id, string Username, string Role);
}
