using ZapCavab.Models;
using ZapCavab.Services;

// ZapCavab — test proqramı: mal siyahısını qurur, CLAUDE.md-dəki test hallarını işə salır
// və nəticələri konsola çap edir. UI (WPF) hələ yoxdur, bu yalnız axtarış məntiqinin yoxlanmasıdır.

// 1) Test üçün kiçik mal anbarı (sonradan Excel-dən import olunacaq)
var mallar = new List<Part>
{
    new() { Id = 1, Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
            PartNameAz = "Ön əyləc altlığı", OemCode = "04465-60310", Price = 45, StockQty = 5 },

    new() { Id = 2, Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
            PartNameAz = "Ön əyləc altlığı (analoq)", OemCode = "04465-60290", Price = 32, StockQty = 0 },

    new() { Id = 3, Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
            PartNameAz = "Ön əyləc altlığı (orijinal)", OemCode = "D1060-JK50A", Price = 68, StockQty = 2 },

    new() { Id = 4, Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
            PartNameAz = "Arxa amortizator", OemCode = "48531-69745", Price = 120, StockQty = 3 },

    new() { Id = 5, Brand = "Toyota", Model = "Camry", YearFrom = 2015, YearTo = 2019,
            PartNameAz = "Yağ filtri", OemCode = "90915-YZZD4", Price = 12, StockQty = 10 },

    new() { Id = 6, Brand = "Hyundai", Model = "Elantra", YearFrom = 2016, YearTo = 2020,
            PartNameAz = "Ön fara", OemCode = "92101-F2000", Price = 95, StockQty = 1 },

    new() { Id = 7, Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
            PartNameAz = "Ön fara", OemCode = "81150-60J51", Price = 210, StockQty = 1 },
};

var logger = new SearchLogger();

// 2) CLAUDE.md-dəki test halları: sorğu -> gözlənilən nəticə sayı
var testler = new (string Sorgu, int GozlenilenSay, string Izah)[]
{
    ("prado 2015 kolodka",        3, "3 ön altlıq"),
    ("PRADO 2015 KOLODKA",        3, "eyni nəticə (böyük hərflər)"),
    ("prado ön əyləc altlığı",    3, "eyni nəticə (tam Azərbaycan adı)"),
    ("прадо колодки",             3, "eyni nəticə (rus/kiril)"),
    ("prado 2005 kolodka",        3, "tapılır + il xəbərdarlığı"),

    // "BEYİN" yenilənməsindən sonra əlavə olunan testlər:
    ("salam prado 2015 kolodka varmi",  3, "stop-word-lər (salam, varmı) baldan çıxılır"),
    ("kolodka qiymeti",                 3, "'qiyməti' stop-word-dür, 'kolodka' təkbaşına axtarır"),
    ("prado 2015 kolodkasi",            3, "'kolodkası' şəkilçili forma, kök uyğunluğu ilə tapılır"),
    ("prado arxa kolodka",              3, "yalnız ön altlıqlar (mövqe cəzası ilə), 'Arxa amortizator' göstərilmir"),
    ("hyundai prado kolodka",           0, "ziddiyyətli marka (Hyundai + Prado) — heç bir mal ikisinə birdən uyğun deyil"),

    // Yeni qayda: hissə adı tanınmayıbsa (yalnız marka/il varsa), heç nə göstərmə.
    // Bu, "yanlış cavab boş cavabdan pisdir" prinsipinə əsaslanır.
    ("prado 2015 turbo",                0, "'turbo' hissə adı deyil, heç nə göstərilmir"),
    ("prado 2015",                      0, "hissə adı ümumiyyətlə yoxdur"),

    // ADDIM 4 — genişlənmiş sinonim lüğəti testləri:
    ("camry yag filtri",                1, "yağ filtri (marka+model+hissə uyğun)"),
    ("amortizator prado",               1, "amortizator (söz sırası fərqli, yenə tapılır)"),
    ("prado fara",                      1, "fara (kataloqa təzə əlavə olunmuş Prado fara)"),
};

Console.WriteLine("=== ZapCavab axtarış testləri ===\n");

var hamisiKecdi = true;

foreach (var test in testler)
{
    var neticeler = SearchService.Search(test.Sorgu, mallar);
    logger.Log(test.Sorgu, neticeler.Count);

    var kecdi = neticeler.Count == test.GozlenilenSay;
    hamisiKecdi &= kecdi;

    Console.WriteLine($"Sorğu: \"{test.Sorgu}\"  ({test.Izah})");
    Console.WriteLine($"  Gözlənilən: {test.GozlenilenSay} | Tapıldı: {neticeler.Count} | {(kecdi ? "✅ PASS" : "❌ FAIL")}");

    if (neticeler.Any(n => n.IlUygunGelmir))
        Console.WriteLine("  (il xəbərdarlığı aktivdir)");

    Console.WriteLine();
}

Console.WriteLine(hamisiKecdi ? "=== Bütün testlər keçdi ✅ ===" : "=== Bəzi testlər uğursuz oldu ❌ ===");

// 3) Nümunə: hazır WhatsApp cavabının necə göründüyünü göstər
Console.WriteLine("\n=== Nümunə hazır cavab ('prado 2015 kolodka' üçün) ===\n");
var numuneNetice = SearchService.Search("prado 2015 kolodka", mallar);
Console.WriteLine(ReplyBuilder.Build(numuneNetice));

// 4) Nümunə: il xəbərdarlığı olan cavab
Console.WriteLine("\n=== Nümunə hazır cavab ('prado 2005 kolodka' üçün, il xəbərdarlığı ilə) ===\n");
var ilXeberdarligiNetice = SearchService.Search("prado 2005 kolodka", mallar);
Console.WriteLine(ReplyBuilder.Build(ilXeberdarligiNetice));

// 5) Nümunə: heç nəticə tapılmayan cavab
Console.WriteLine("\n=== Nümunə hazır cavab ('prado 2015 turbo' üçün) ===\n");
var boshNetice = SearchService.Search("prado 2015 turbo", mallar);
Console.WriteLine(ReplyBuilder.Build(boshNetice));

// 6) Axtarış jurnalından tapılmayan sorğuları göstər (statistika üçün nümunə)
Console.WriteLine("\n=== Tapılmayan axtarışlar ===");
var tapilmayanlar = logger.TapilmayanlariGetir();
if (tapilmayanlar.Count == 0)
    Console.WriteLine("(yoxdur)");
else
    foreach (var s in tapilmayanlar)
        Console.WriteLine($"  - {s}");
