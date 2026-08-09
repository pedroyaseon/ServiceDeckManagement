using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServiceDeckManagement.Contracts.Manager;
using ServiceDeckManagement.Infrastructure.LocalProtocol;

namespace ServiceDeckManagement.ManagerTests;

public sealed class LocalProtocolTests
{
    [Fact]
    public async Task Frame_RoundTripsPartialReads()
    {
        var payload = "mensagem UTF-8: serviço"u8.ToArray();
        await using var stream = new MemoryStream();
        await LengthPrefixedFrame.WriteAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;

        var result = await LengthPrefixedFrame.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task Frame_RejectsOversizedDeclaration()
    {
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(
            header, LengthPrefixedFrame.MaximumPayloadBytes + 1u);
        await using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LengthPrefixedFrame.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Frame_RejectsTruncatedPayload()
    {
        await using var stream = new MemoryStream([4, 0, 0, 0, 1, 2]);

        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            LengthPrefixedFrame.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public void Handshake_AuthenticatesBothSides()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var challenge = ManagerHandshake.CreateChallenge(key);

        ManagerHandshake.ValidateChallenge(key, challenge);
        var authentication = ManagerHandshake.CreateAuthentication(key, challenge);
        ManagerHandshake.ValidateAuthentication(key, challenge, authentication);
    }

    [Fact]
    public void Handshake_RejectsProofFromAnotherKey()
    {
        var challenge = ManagerHandshake.CreateChallenge(RandomNumberGenerator.GetBytes(32));

        Assert.Throws<UnauthorizedAccessException>(() =>
            ManagerHandshake.ValidateChallenge(RandomNumberGenerator.GetBytes(32), challenge));
    }

    [Fact]
    public void Handshake_RejectsAuthenticationForAnotherNonce()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var first = ManagerHandshake.CreateChallenge(key);
        var second = ManagerHandshake.CreateChallenge(key);
        var authentication = ManagerHandshake.CreateAuthentication(key, second);

        Assert.Throws<UnauthorizedAccessException>(() =>
            ManagerHandshake.ValidateAuthentication(key, first, authentication));
    }

    [Fact]
    public void Json_RejectsUnknownProperties()
    {
        var json = """
            {"protocolVersion":1,"requestId":"00000000-0000-0000-0000-000000000000","operation":"ping","payload":{},"extra":true}
            """u8.ToArray();

        Assert.Throws<JsonException>(() => ManagerJson.Deserialize<ManagerRequestV1>(json));
    }

    [Fact]
    public void Json_RejectsDuplicateProperties()
    {
        var json = Encoding.UTF8.GetBytes(
            "{\"protocolVersion\":1,\"protocolVersion\":1," +
            "\"requestId\":\"00000000-0000-0000-0000-000000000000\"," +
            "\"operation\":\"ping\",\"payload\":{}}");

        Assert.Throws<JsonException>(() => ManagerJson.Deserialize<ManagerRequestV1>(json));
    }
}
