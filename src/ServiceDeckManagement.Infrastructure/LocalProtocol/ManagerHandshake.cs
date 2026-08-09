using System.Security.Cryptography;
using System.Text;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Contracts.Versioning;

namespace ServiceDeckManagement.Infrastructure.LocalProtocol;

public static class ManagerHandshake
{
    public static ManagerChallengeV1 CreateChallenge(ReadOnlySpan<byte> key)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new()
        {
            ProtocolVersion = ContractVersions.LocalProtocol,
            Nonce = nonce,
            ServerProof = ComputeProof(key, "server", nonce)
        };
    }

    public static ManagerAuthenticationV1 CreateAuthentication(
        ReadOnlySpan<byte> key,
        ManagerChallengeV1 challenge)
    {
        ValidateChallenge(key, challenge);
        return new()
        {
            ProtocolVersion = ContractVersions.LocalProtocol,
            Nonce = challenge.Nonce,
            ClientProof = ComputeProof(key, "client", challenge.Nonce)
        };
    }

    public static void ValidateChallenge(
        ReadOnlySpan<byte> key,
        ManagerChallengeV1 challenge)
    {
        ValidateVersionAndNonce(challenge.ProtocolVersion, challenge.Nonce);
        ValidateProof(
            challenge.ServerProof,
            ComputeProof(key, "server", challenge.Nonce),
            "A identidade do Manager não foi confirmada.");
    }

    public static void ValidateAuthentication(
        ReadOnlySpan<byte> key,
        ManagerChallengeV1 challenge,
        ManagerAuthenticationV1 authentication)
    {
        ValidateVersionAndNonce(authentication.ProtocolVersion, authentication.Nonce);
        if (!string.Equals(challenge.Nonce, authentication.Nonce, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("O nonce da sessão não corresponde ao desafio.");
        }

        ValidateProof(
            authentication.ClientProof,
            ComputeProof(key, "client", challenge.Nonce),
            "O cliente local não foi autenticado.");
    }

    private static string ComputeProof(
        ReadOnlySpan<byte> key,
        string side,
        string nonce)
    {
        if (key.Length != 32)
        {
            throw new CryptographicException("A chave do transporte deve possuir 256 bits.");
        }

        var message = Encoding.ASCII.GetBytes(
            $"ServiceDeckManagement|v{ContractVersions.LocalProtocol}|{side}|{nonce}");
        return Convert.ToBase64String(HMACSHA256.HashData(key, message));
    }

    private static void ValidateVersionAndNonce(int version, string nonce)
    {
        if (version != ContractVersions.LocalProtocol ||
            string.IsNullOrWhiteSpace(nonce) ||
            nonce.Length > 64)
        {
            throw new UnauthorizedAccessException("O desafio local é inválido.");
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(nonce);
        }
        catch (FormatException exception)
        {
            throw new UnauthorizedAccessException("O nonce não usa Base64 válido.", exception);
        }

        if (decoded.Length != 32)
        {
            throw new UnauthorizedAccessException("O nonce deve possuir 256 bits.");
        }
    }

    private static void ValidateProof(string actual, string expected, string message)
    {
        byte[] actualBytes;
        try
        {
            actualBytes = Convert.FromBase64String(actual);
        }
        catch (FormatException exception)
        {
            throw new UnauthorizedAccessException(message, exception);
        }

        var expectedBytes = Convert.FromBase64String(expected);
        if (actualBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
        {
            throw new UnauthorizedAccessException(message);
        }
    }
}
