using System.Security.Cryptography;
using ServiceDeckManagement.Infrastructure.Paths;

namespace ServiceDeckManagement.Infrastructure.LocalProtocol;

/// <summary>
/// Leitura estrita da chave criada e protegida pelo Manager. A API nunca cria
/// nem altera o material de autenticação local.
/// </summary>
public sealed class DpapiTransportKeyReader(ProductPaths paths) : ITransportKeyProvider
{
    private static readonly byte[] Entropy = "ServiceDeckManagement.Manager.Transport.v1"u8.ToArray();

    public async Task<byte[]> GetKeyAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("A proteção DPAPI requer Windows.");
        }

        if (!File.Exists(paths.ManagerTransportKey))
        {
            throw new IOException("A chave de transporte do Manager ainda não existe.");
        }

        var attributes = File.GetAttributes(paths.ManagerTransportKey);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException("A chave local deve ser um arquivo regular.");
        }

        var encrypted = await File.ReadAllBytesAsync(paths.ManagerTransportKey, cancellationToken).ConfigureAwait(false);
        if (encrypted.Length is <= 0 or > 4_096)
        {
            throw new InvalidDataException("O material de autenticação local é inválido.");
        }

        var key = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.LocalMachine);
        if (key.Length == 32)
        {
            return key;
        }

        CryptographicOperations.ZeroMemory(key);
        throw new InvalidDataException("A chave local possui tamanho inválido.");
    }
}
