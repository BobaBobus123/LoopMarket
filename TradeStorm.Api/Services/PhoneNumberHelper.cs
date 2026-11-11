namespace TradeStorm.Api.Services;

/// <summary>
/// Normalizes stored and displayed phone numbers so they never contain duplicate '+' signs.
/// </summary>
public static class PhoneNumberHelper
{
    public static string? Normalize(string? raw)
    {
        if (raw == null) return null;
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var trimmed = raw.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length >= 9)
        {
            return "+" + digits;
        }

        var withoutPlus = trimmed.TrimStart('+').Trim();
        return string.IsNullOrEmpty(withoutPlus) ? trimmed : "+" + withoutPlus;
    }
}
