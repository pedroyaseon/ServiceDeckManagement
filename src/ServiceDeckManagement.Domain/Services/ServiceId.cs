namespace ServiceDeckManagement.Domain.Services;

/// <summary>
/// Identificador canônico e imutável de um serviço gerenciado.
/// </summary>
public readonly record struct ServiceId
{
    public const int MaximumLength = 63;

    private ServiceId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? value, out ServiceId serviceId)
    {
        serviceId = default;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumLength ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            !IsAsciiLowerLetterOrDigit(value[0]) ||
            !IsAsciiLowerLetterOrDigit(value[^1]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!IsAsciiLowerLetterOrDigit(character) && character != '-')
            {
                return false;
            }
        }

        serviceId = new ServiceId(value);
        return true;
    }

    public static ServiceId Create(string value) =>
        TryCreate(value, out var serviceId)
            ? serviceId
            : throw new ArgumentException(
                "O identificador do serviço é inválido.",
                nameof(value));

    public override string ToString() => Value ?? string.Empty;

    private static bool IsAsciiLowerLetterOrDigit(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';
}
