using ZapCavab.Helpers;

namespace ZapCavab.Data;

// Eyni malın müxtəlif adlandırılma yollarını (Azərbaycan, rus/kiril, danışıq dili)
// bir "əsas söz"ə (kanonik sözə) çevirir. Məsələn: "əyləc altlığı" da, "колодки" da
// axtarışda "kolodka" kimi qəbul olunur.
public static class SynonymDictionary
{
    // TƏK SÖZLƏR: söz mətnin harasında olursa-olsun, tapılan kimi əsas sözlə əvəz olunur.
    // Əsasən rus/kiril sözlər və ya tamam fərqli görünən Azərbaycan sözləri üçün istifadə olunur
    // (bunlara "kök uyğunluğu" tətbiq etmək mənasızdır, çünki tamam başqa hərflərdən ibarətdirlər).
    private static readonly Dictionary<string, string> TekSozler = new()
    {
        // Əyləc altlığı
        ["колодки"] = "kolodka",
        ["колодка"] = "kolodka",

        // Prado
        ["прадо"] = "prado",

        // Amortizator
        ["амортизатор"] = "amortizator",

        // Əyləc diski
        ["диск"] = "disk",

        // Radiator
        ["радиатор"] = "radiator",

        // Fara
        ["фара"] = "fara",

        // Bufer
        ["бампер"] = "bufer",

        // Güzgü
        ["ayna"] = "guzgu",
        ["зеркало"] = "guzgu",

        // Akkumulyator
        ["akb"] = "akkumulyator",
        ["batareya"] = "akkumulyator",
        ["аккумулятор"] = "akkumulyator",

        // Topça / şarnir
        ["sarnir"] = "topca",

        // Reyka
        ["рейка"] = "reyka",

        // Turbina
        ["турбина"] = "turbina",

        // Generator
        ["генератор"] = "generator",

        // Starter
        ["стартер"] = "starter",

        // Rulman (podşipnik)
        ["podsipnik"] = "rulman",
        ["подшипник"] = "rulman",

        // Tyaqa
        ["тяга"] = "tyaqa",
        ["наконечник"] = "tyaqa",

        // Şlanq
        ["шланг"] = "slanq",

        // Kəmər
        ["ремень"] = "kemer",

        // Şam (alışdırıcı)
        ["свеча"] = "sam",
        ["свечи"] = "sam",
    };

    // ÇOXSÖZLÜ QRUPLAR: hər qrupda bir neçə "kök" söz var. Bu köklərin HAMISI mətndə
    // (İSTƏNİLƏN SIRADA, aralarında başqa sözlər olsa belə) tapılarsa, hamısı çıxarılıb
    // yerinə tək kanonik söz qoyulur. Kök sözlər qəsdən qısa saxlanılıb ki, TextHelper.KokUygunGelir
    // vasitəsilə şəkilçili formaları da (kolodkasi, filtrini və s.) tutsun.
    private static readonly List<(string[] Kokler, string Kanonik)> CoxSozluQruplar = new()
    {
        // Əyləc altlığı (kolodka) — "əyləc ön altlığı" da, "ön əyləc altlığı" da eyni tanınır
        (new[] { "eylec", "altl" }, "kolodka"),

        // Əyləc diski
        (new[] { "eylec", "disk" }, "disk"),
        (new[] { "tormoz", "disk" }, "disk"),

        // Yağ filtri
        (new[] { "yag", "filt" }, "yagfiltri"),
        (new[] { "маслян", "фильтр" }, "yagfiltri"),

        // Hava filtri
        (new[] { "hava", "filt" }, "havafiltri"),
        (new[] { "воздуш", "фильтр" }, "havafiltri"),

        // Salon filtri (kondisioner filtri ilə eyni şey sayılır)
        (new[] { "salon", "filt" }, "salonfiltri"),
        (new[] { "kondisioner", "filt" }, "salonfiltri"),
        (new[] { "салон", "фильтр" }, "salonfiltri"),

        // Yanacaq filtri
        (new[] { "yanacaq", "filt" }, "yanacaqfiltri"),
        (new[] { "топливн", "фильтр" }, "yanacaqfiltri"),

        // Kəmər (vaxt/rulon kəməri)
        (new[] { "vaxt", "kemer" }, "kemer"),
        (new[] { "rulon", "kemer" }, "kemer"),

        // Topça / şarnir
        (new[] { "шаров" }, "topca"),

        // Reyka (sükan qutusu)
        (new[] { "sukan", "qutu" }, "reyka"),
        (new[] { "рулев", "рейка" }, "reyka"),

        // Tyaqa (rulevaya tyaqa)
        (new[] { "рулев", "тяга" }, "tyaqa"),
    };

    // Verilmiş normalizasiya olunmuş mətndəki bütün məlum sinonimləri əsas sözlə əvəz edir.
    public static string ToCanonical(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
            return normalizedText;

        var sozler = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // 1) Tək sözlərin əvəzlənməsi (sıra fərq etmir, hər söz ayrıca yoxlanır)
        for (var i = 0; i < sozler.Count; i++)
        {
            if (TekSozler.TryGetValue(sozler[i], out var kanonikSoz))
                sozler[i] = kanonikSoz;
        }

        // 2) Çoxsözlü qruplar — sıradan asılı olmadan, kök uyğunluğu ilə
        foreach (var qrup in CoxSozluQruplar)
        {
            var tapilanSozler = new List<string>();
            var koklerinHamisiVar = true;

            foreach (var kok in qrup.Kokler)
            {
                var tapilan = sozler.FirstOrDefault(s => !tapilanSozler.Contains(s) && TextHelper.KokUygunGelir(kok, s));
                if (tapilan == null)
                {
                    koklerinHamisiVar = false;
                    break;
                }

                tapilanSozler.Add(tapilan);
            }

            if (!koklerinHamisiVar)
                continue;

            foreach (var s in tapilanSozler)
                sozler.Remove(s);

            sozler.Add(qrup.Kanonik);
        }

        return string.Join(' ', sozler);
    }
}
