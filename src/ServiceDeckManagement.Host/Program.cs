using System.Text;
using ServiceDeckManagement.Host;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

return await ServiceHostProgram.RunAsync(args).ConfigureAwait(false);
