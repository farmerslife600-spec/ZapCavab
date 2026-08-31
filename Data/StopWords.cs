namespace ZapCavab.Data;

// Müştərinin yazdığı, amma malın özü ilə heç bir əlaqəsi olmayan "nəzakət/dolğu" sözlər.
// Məsələn "salam prado kolodka varmı?" sorğusunda "salam" və "varmı" heç nəyi
// dəyişməməlidir — axtarış sanki "prado kolodka" yazılıbmış kimi işləməlidir.
public static class StopWords
{
    private static readonly HashSet<string> Sozler = new()
    {
        "salam", "salamlar", "sagol", "sagolun",
        "varmi", "var", "yoxdur",
        "neceye", "nece", "qiymeti", "qiymet",
        "xahis", "zehmet", "olmaz", "olar",
        "lazimdir", "axtariram", "isteyirem",
        "bilmek", "deyin", "mumkunse", "mence",
        "bir", "ede", "bilerem", "ucun", "mene",
    };

    // Verilmiş söz siyahısından bütün stop-word-ləri çıxarıb qalanları qaytarır.
    public static List<string> Cixar(List<string> sozler)
    {
        return sozler.Where(s => !Sozler.Contains(s)).ToList();
    }
}
