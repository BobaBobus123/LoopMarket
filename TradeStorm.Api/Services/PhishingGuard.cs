using System;
using System.Text.RegularExpressions;

namespace TradeStorm.Api.Services;

public static class PhishingGuard
{
    private static readonly Regex PhishingUrlRegex = new(
        @"(https?:\/\/)?([a-zA-Z0-9-]+\.)*(olx(-|\.)?(pay|delivery|safe|checkout)|nova(-|\.)?poshta(-|\.)?(track|pay|order)|meest(-|\.)?pay|ukrposhta(-|\.)?pay|safe(-|\.)?deal|card(-|\.)?verify|secure(-|\.)?(ua|pay|deal|bank)|privat(-|\.)?(pay|verify)|mono(-|\.)?(pay|claim)|loop(-|\.)?(pay|checkout))\.[a-zA-Z]{2,}(\/[^\s]*)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex SuspiciousShorteners = new(
        @"(https?:\/\/)?(bit\.ly|tinyurl\.com|cutt\.ly|is\.gd|t\.co|rb\.gy|goo\.gl|clck\.ru)\/[a-zA-Z0-9_\-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex ScamPhrasesRegex = new(
        @"(перейдіть (за|по) посиланн[яі]|отрима(ти|йте) кошти за посиланн[яі]|введіть (дані|номер) карт(ки|и) (для|та) зарахування|підтверд(іть|ження) (оплати|коштів) за посиланн[яі]|оплата вже внесена.*перейдіть|переведіть кошти на картку)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public static bool IsPhishingOrScam(string? text, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        if (PhishingUrlRegex.IsMatch(text))
        {
            reason = "Виявлено фішингове посилання, що маскується під платіжні системи або служби доставки";
            return true;
        }

        if (SuspiciousShorteners.IsMatch(text))
        {
            reason = "Використання скорочувачів посилань суворо заборонено правилами безпеки Loop Market";
            return true;
        }

        if (ScamPhrasesRegex.IsMatch(text))
        {
            reason = "Текст містить типові фрази шахрайських схем з виманювання банківських даних";
            return true;
        }

        return false;
    }
}
