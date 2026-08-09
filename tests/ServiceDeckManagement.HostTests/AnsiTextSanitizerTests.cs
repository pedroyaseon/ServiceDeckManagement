using ServiceDeckManagement.Host.Logging;

namespace ServiceDeckManagement.HostTests;

public sealed class AnsiTextSanitizerTests
{
    [Theory]
    [InlineData("\u001b[32mtexto\u001b[0m", "texto")]
    [InlineData("antes\u001b]0;título\a depois", "antes depois")]
    [InlineData("a\0b\u0001c", "abc")]
    [InlineData("inválido\ufffd", "inválido")]
    [InlineData("texto normal", "texto normal")]
    public void Sanitize_RemovesTerminalAndInvalidCharacters(
        string input,
        string expected)
    {
        Assert.Equal(expected, AnsiTextSanitizer.Sanitize(input));
    }
}
