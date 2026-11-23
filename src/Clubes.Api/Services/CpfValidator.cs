namespace Clubes.Api.Services;

public static class CpfValidator
{
    public static string NormalizeAndValidate(string value)
    {
        var cpf = new string(value.Where(character => character is >= '0' and <= '9').ToArray());
        if (cpf.Length != 11 || cpf.All(character => character == cpf[0]))
        {
            throw new ArgumentException("Informe um CPF válido.", nameof(value));
        }

        var first = CalculateDigit(cpf[..9], 10);
        var second = CalculateDigit(cpf[..9] + first, 11);
        if (cpf[9] - '0' != first || cpf[10] - '0' != second)
        {
            throw new ArgumentException("Informe um CPF válido.", nameof(value));
        }

        return cpf;
    }

    private static int CalculateDigit(string digits, int weight)
    {
        var sum = 0;
        foreach (var digit in digits)
        {
            sum += (digit - '0') * weight--;
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
