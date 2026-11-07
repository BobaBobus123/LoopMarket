using System;
using System.Collections.Generic;

namespace TradeStorm.Api.Services;

/// <summary>
/// Сервіс двонаправленого безпечного кодування ID оголошень (Anti-Scraping / Anti-Enumeration).
/// Перетворює числові auto-increment ID бази даних (1, 2, 3...) у 7-значні буквено-цифрові коди (наприклад: 5mPyHdL).
/// Запобігає послідовному перебору та парсингу бази конкурентами і ботами.
/// </summary>
public static class ListingIdEncoder
{
    private const int Rounds = 4;
    private static readonly uint[] Keys = { 0x79B9C9E1, 0x4D2F1B85, 0x9E3779B9, 0x1B873593 };
    
    // Алфавіт з 56 символів без неоднозначних знаків (без 0, O, 1, I, l)
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";
    private static readonly int Base = Alphabet.Length; // 56

    /// <summary>
    /// Кодує числовий ID оголошення у безпечний 7-значний буквено-цифровий код
    /// </summary>
    public static string Encode(int id)
    {
        if (id <= 0) return string.Empty;
        uint enc = Encrypt32((uint)id);
        int chk = (int)((enc * 37 + 11) % (uint)Base);
        string bStr = IntToBase(enc).PadLeft(6, Alphabet[0]);
        return bStr + Alphabet[chk];
    }

    /// <summary>
    /// Декодує 7-значний буквено-цифровий код назад у числовий ID.
    /// Повертає null, якщо код невалідний або пошкоджено контрольну суму.
    /// </summary>
    public static int? Decode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 7)
            return null;

        code = code.Trim();
        char chkChar = code[^1];
        string bStr = code[..^1];
        uint? enc = BaseToInt(bStr);
        if (enc == null)
            return null;

        int expectedChk = (int)((enc.Value * 37 + 11) % (uint)Base);
        if (Alphabet[expectedChk] != chkChar)
            return null;

        uint dec = Decrypt32(enc.Value);
        return (int)dec;
    }

    /// <summary>
    /// Універсальна спроба розпізнати ID (підтримує як новий буквено-цифровий код, так і зворотну сумісність з числовими ID)
    /// </summary>
    public static bool TryDecode(string? input, out int id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string trimmed = input.Trim();

        // 1. Спочатку перевіряємо буквено-цифровий код (основний безпечний шлях)
        var decoded = Decode(trimmed);
        if (decoded.HasValue && decoded.Value > 0)
        {
            id = decoded.Value;
            return true;
        }

        // 2. Зворотна сумісність для числових ID
        if (int.TryParse(trimmed, out int numId) && numId > 0)
        {
            id = numId;
            return true;
        }

        return false;
    }

    private static uint RoundFunc(uint val, uint key)
    {
        uint k = (val ^ key) & 0xFFFF;
        k = k * 0x85ebca6b;
        k ^= (k >> 13);
        k = k * 0xc2b2ae35;
        k ^= (k >> 16);
        return k & 0xFFFF;
    }

    private static uint Encrypt32(uint n)
    {
        uint left = (n >> 16) & 0xFFFF;
        uint right = n & 0xFFFF;
        for (int i = 0; i < Rounds; i++)
        {
            uint newLeft = right;
            uint newRight = (left ^ RoundFunc(right, Keys[i])) & 0xFFFF;
            left = newLeft;
            right = newRight;
        }
        return (left << 16) | right;
    }

    private static uint Decrypt32(uint c)
    {
        uint left = (c >> 16) & 0xFFFF;
        uint right = c & 0xFFFF;
        for (int i = Rounds - 1; i >= 0; i--)
        {
            uint prevRight = left;
            uint prevLeft = (right ^ RoundFunc(left, Keys[i])) & 0xFFFF;
            left = prevLeft;
            right = prevRight;
        }
        return (left << 16) | right;
    }

    private static string IntToBase(uint num)
    {
        if (num == 0) return Alphabet[0].ToString();
        var chars = new List<char>();
        while (num > 0)
        {
            chars.Add(Alphabet[(int)(num % (uint)Base)]);
            num /= (uint)Base;
        }
        chars.Reverse();
        return new string(chars.ToArray());
    }

    private static uint? BaseToInt(string s)
    {
        try
        {
            uint num = 0;
            foreach (char ch in s)
            {
                int idx = Alphabet.IndexOf(ch);
                if (idx == -1) return null;
                num = checked(num * (uint)Base + (uint)idx);
            }
            return num;
        }
        catch
        {
            return null;
        }
    }
}
