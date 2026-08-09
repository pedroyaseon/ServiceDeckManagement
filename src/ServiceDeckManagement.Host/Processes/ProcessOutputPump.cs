using System.Text;
using ServiceDeckManagement.Host.Logging;

namespace ServiceDeckManagement.Host.Processes;

internal static class ProcessOutputPump
{
    private const int ReadBufferCharacters = 4_096;
    private const int MaximumLineCharacters = 16_384;

    internal static async Task RunAsync(
        TextReader reader,
        ServiceLogSource source,
        IServiceLogSink sink,
        CancellationToken cancellationToken)
    {
        var readBuffer = new char[ReadBufferCharacters];
        var line = new StringBuilder();

        while (true)
        {
            var read = await reader.ReadAsync(
                readBuffer.AsMemory(),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                var character = readBuffer[index];
                if (character == '\n')
                {
                    await FlushAsync(line, source, sink, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                if (character != '\r')
                {
                    line.Append(character);
                }

                if (line.Length >= MaximumLineCharacters)
                {
                    line.Append(" [continua]");
                    await FlushAsync(line, source, sink, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        await FlushAsync(line, source, sink, cancellationToken).ConfigureAwait(false);
    }

    private static async Task FlushAsync(
        StringBuilder line,
        ServiceLogSource source,
        IServiceLogSink sink,
        CancellationToken cancellationToken)
    {
        if (line.Length == 0)
        {
            return;
        }

        var value = line.ToString();
        line.Clear();
        await sink.WriteAsync(source, value, cancellationToken).ConfigureAwait(false);
    }
}
