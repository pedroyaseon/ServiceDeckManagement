using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class ManagerSecurityConfigurationWriter(ProductPaths paths)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public void Save(ManagerSecurityOptions options)
    {
        var normalized = ManagerSecurityOptionsValidator.NormalizeAndValidate(options);
        Directory.CreateDirectory(paths.Configuration);
        EnsureRegularDirectory(paths.Configuration);
        var path = Path.Combine(paths.Configuration, "manager-security.json");
        if (File.Exists(path)) EnsureRegularFile(path);

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var payload = Utf8.GetBytes(JsonSerializer.Serialize(
                new ManagerSecurityFile
                {
                    SchemaVersion = 1,
                    ApiClientSid = normalized.ApiClientSid,
                    LauncherClientSid = normalized.LauncherClientSid
                },
                JsonOptions) + "\n");
            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4_096,
                       FileOptions.WriteThrough))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void EnsureRegularDirectory(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("A pasta de configuração não pode ser um reparse point.");
        }
    }

    private static void EnsureRegularFile(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("A configuração de segurança deve ser um arquivo regular.");
        }
    }

    private sealed record ManagerSecurityFile
    {
        public int SchemaVersion { get; init; }
        public string? ApiClientSid { get; init; }
        public string? LauncherClientSid { get; init; }
    }
}
