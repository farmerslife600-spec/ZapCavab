using System.Text;
using System.Text.RegularExpressions;

namespace ZapCavab.Helpers;

// Axtarış mətnini təmizləyib müqayisə üçün hazır formaya salan köməkçi funksiyalar.
public static class TextHelper
{
    // Azərbaycan hərflərini latın əlifbasının sadə hərflərinə çevirir ki,
    // "Kolodka" ilə "KOLODKA" ilə "kolodka" eyni şəkildə axtarılsın.
    private static readonly Dictionary<char, char> HerfXeritesi = new()
    {
        ['ə'] = 'e',
        ['ş'] = 's',
        ['ç'] = 'c',
        ['ğ'] = 'g',
        ['ö'] = 'o',
        ['ü'] = 'u',
        ['ı'] = 'i',
    };

    // İl axtarmaq üçün: 1950-2049 arası 4 rəqəmli ədədləri tapır (CLAUDE.md-də göstərilən aralıq).
    private static readonly Regex IlRegex = new(@"\b(19[5-9]\d|20[0-4]\d)\b", RegexOptions.Compiled);

    // Mətni kiçik hərfə salır, Azərbaycan hərflərini əvəz edir və lazımsız işarələri silir.
    // Kiril (rus/rus dilində yazılmış) hərflərə toxunmur — onları SynonymDictionary öz üzərinə götürür.
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var kicik = input.ToLowerInvariant();
        var sb = new StringBuilder(kicik.Length);

        foreach (var ch in kicik)
        {
            if (HerfXeritesi.TryGetValue(ch, out var yeni))
            {
                sb.Append(yeni);
            }
            else if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            {
                // Hərf, rəqəm və boşluqları saxla, qalan bütün işarələri (?, !, ., və s.) at
                sb.Append(ch);
            }
            else
            {
                sb.Append(' ');
            }
        }

        // Bir neçə boşluğu tək boşluğa endir
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    // Mətnin içindən il (1950-2049) tapır, tapılarsa onu qaytarır.
    public static int? ExtractYear(string normalizedText)
    {
        var uygunluq = IlRegex.Match(normalizedText);
        return uygunluq.Success ? int.Parse(uygunluq.Value) : null;
    }

    // İl rəqəmini mətndən çıxarıb qalan sözləri qaytarır (söz siyahısına bölmək üçün).
    public static string RemoveYear(string normalizedText)
    {
        var ilsiz = IlRegex.Replace(normalizedText, " ");
        return Regex.Replace(ilsiz, @"\s+", " ").Trim();
    }

    // Mətni sözlərə bölür, təkrarları silir (məsələn "prado prado" -> ["prado"]).
    public static List<string> SplitWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Distinct()
                    .ToList();
    }

    // İki sözün "eyni kökdən" olub-olmadığını yoxlayır — Azərbaycan dilində sözlərə
    // şəkilçi əlavə olunur (kolodka -> kolodkanı, kolodkası, kolodkadan...), ona görə
    // tam bərabərlik axtarmaq çox vaxt "tapılmadı" nəticəsi verir.
    // 4 hərfdən qısa sözlərdə tam bərabərlik tələb olunur — qısa sözlərdə (məs. "on", "sag")
    // başlanğıc-uyğunluq yoxlaması təsadüfi səhv uyğunluq riski yaradar.
    public static bool KokUygunGelir(string birinci, string ikinci)
    {
        if (birinci.Length < 4 || ikinci.Length < 4)
            return birinci == ikinci;

        return birinci.StartsWith(ikinci, StringComparison.Ordinal) ||
               ikinci.StartsWith(birinci, StringComparison.Ordinal);
    }
}
