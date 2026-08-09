using System.Text;

namespace ServiceDeckManagement.Host.Logging;

/// <summary>
/// Remove sequências de terminal, controles invisíveis e caracteres inválidos.
/// </summary>
public static class AnsiTextSanitizer
{
    private enum ParserState
    {
        Text,
        Escape,
        ControlSequence,
        OperatingSystemCommand,
        OperatingSystemCommandEscape,
        StringCommand,
        StringCommandEscape
    }

    public static string Sanitize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var output = new StringBuilder(value.Length);
        var state = ParserState.Text;

        foreach (var character in value)
        {
            switch (state)
            {
                case ParserState.Text:
                    if (character == '\u001b')
                    {
                        state = ParserState.Escape;
                    }
                    else if (character == '\u009b')
                    {
                        state = ParserState.ControlSequence;
                    }
                    else if (character == '\ufffd')
                    {
                        continue;
                    }
                    else if (character == '\t' || !char.IsControl(character))
                    {
                        output.Append(character);
                    }

                    break;

                case ParserState.Escape:
                    state = character switch
                    {
                        '[' => ParserState.ControlSequence,
                        ']' => ParserState.OperatingSystemCommand,
                        'P' or 'X' or '^' or '_' => ParserState.StringCommand,
                        _ => ParserState.Text
                    };
                    break;

                case ParserState.ControlSequence:
                    if (character is >= '@' and <= '~')
                    {
                        state = ParserState.Text;
                    }

                    break;

                case ParserState.OperatingSystemCommand:
                    if (character == '\a')
                    {
                        state = ParserState.Text;
                    }
                    else if (character == '\u001b')
                    {
                        state = ParserState.OperatingSystemCommandEscape;
                    }

                    break;

                case ParserState.OperatingSystemCommandEscape:
                    state = character == '\\'
                        ? ParserState.Text
                        : ParserState.OperatingSystemCommand;
                    break;

                case ParserState.StringCommand:
                    if (character == '\u001b')
                    {
                        state = ParserState.StringCommandEscape;
                    }

                    break;

                case ParserState.StringCommandEscape:
                    state = character == '\\'
                        ? ParserState.Text
                        : ParserState.StringCommand;
                    break;

                default:
                    throw new InvalidOperationException("Estado de sanitização desconhecido.");
            }
        }

        return output.ToString().TrimEnd();
    }
}
