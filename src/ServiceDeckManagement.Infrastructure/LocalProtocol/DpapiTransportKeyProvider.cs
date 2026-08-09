using System.Security.Cryptography;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.LocalProtocol;

/// <summary>
/// Chave local protegida por DPAPI da máquina e por ACL do diretório do Manager.
/// </summary>
public sealed class DpapiTransportKeyProvider(ProductPaths paths) : ITransportKeyProvider, IDisposable
{
    private static readonly byte[] Entropy =
        "ServiceDeckManagement.Manager.Transport.v1"u8.ToArray();
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<byte[]> GetKeyAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("A proteção DPAPI requer Windows.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(paths.ManagerData);
            EnsureRegularDirectory(paths.ManagerData);
            if (File.Exists(paths.ManagerTransportKey))
            {
                EnsureRegularFile(paths.ManagerTransportKey);
                var encrypted = await File.ReadAllBytesAsync(
                    paths.ManagerTransportKey, cancellationToken).ConfigureAwait(false);
                if (encrypted.Length is <= 0 or > 4_096)
                {
                    throw new InvalidDataException("O material de autenticação local é inválido.");
                }

                var key = ProtectedData.Unprotect(
                    encrypted, Entropy, DataProtectionScope.LocalMachine);
                if (key.Length != 32)
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw new InvalidDataException("A chave local possui tamanho inválido.");
                }

                return key;
            }

            var created = RandomNumberGenerator.GetBytes(32);
            var protectedValue = ProtectedData.Protect(
                created, Entropy, DataProtectionScope.LocalMachine);
            var temporary = $"{paths.ManagerTransportKey}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var stream = new FileStream(
                                 temporary,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 4_096,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(protectedValue, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporary, paths.ManagerTransportKey, overwrite: false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedValue);
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }

            return created;
        }
        finally
        {
            gate.Release();
        }
    }

    private static void EnsureRegularDirectory(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("O diretório da chave não pode ser reparse point.");
        }
    }

    private static void EnsureRegularFile(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("A chave local deve ser um arquivo regular.");
        }
    }

    public void Dispose() => gate.Dispose();
}
