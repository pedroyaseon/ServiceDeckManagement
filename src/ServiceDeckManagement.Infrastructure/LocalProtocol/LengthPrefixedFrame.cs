using System.Buffers.Binary;

namespace ServiceDeckManagement.Infrastructure.LocalProtocol;

/// <summary>
/// Frame v1: uint32 little-endian seguido de até 64 KiB.
/// </summary>
public static class LengthPrefixedFrame
{
    public const int MaximumPayloadBytes = 65_536;

    public static async Task WriteAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (payload.Length is <= 0 or > MaximumPayloadBytes)
        {
            throw new InvalidDataException("O frame possui tamanho inválido.");
        }

        var header = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(header, checked((uint)payload.Length));
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<byte[]> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(uint)];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length is 0 or > MaximumPayloadBytes)
        {
            throw new InvalidDataException("O frame excede o limite de 64 KiB.");
        }

        var payload = new byte[checked((int)length)];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("O frame foi encerrado antes do tamanho declarado.");
            }

            offset += read;
        }
    }
}
