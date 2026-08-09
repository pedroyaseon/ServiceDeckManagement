using System.Diagnostics;
using System.Text;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

if (args.Length == 0)
{
    return 2;
}

switch (args[0])
{
    case "--emit":
        Console.WriteLine("\u001b[32msaída limpa\u001b[0m");
        Console.Error.WriteLine("erro controlado");
        Console.WriteLine(string.Join('|', args.Skip(1)));
        return 7;

    case "--wait":
        await Task.Delay(Timeout.InfiniteTimeSpan);
        return 0;

    case "--spawn-child" when args.Length == 2:
        var executable = Environment.ProcessPath ??
            throw new InvalidOperationException("Executável de teste indisponível.");
        using (var child = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "--wait" }
        }) ?? throw new InvalidOperationException("Falha ao iniciar processo filho."))
        {
            await File.WriteAllTextAsync(
                args[1],
                child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }

        return 0;

    case "--spawn-child-and-exit" when args.Length == 2:
        var childExecutable = Environment.ProcessPath ??
            throw new InvalidOperationException("Executável de teste indisponível.");
        using (var child = Process.Start(new ProcessStartInfo
        {
            FileName = childExecutable,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "--wait" }
        }) ?? throw new InvalidOperationException("Falha ao iniciar processo filho."))
        {
            await File.WriteAllTextAsync(
                args[1],
                child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return 9;

    default:
        return 2;
}
