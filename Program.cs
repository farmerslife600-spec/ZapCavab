using ZapCavab.Data;
using ZapCavab.Models;
using ZapCavab.Services;

// ZapCavab — test proqramı: bazanı qurur, CLAUDE.md-dəki test hallarını işə salır
// və nəticələri konsola çap edir. UI (WPF) hələ yoxdur, bu yalnız axtarış məntiqinin yoxlanmasıdır.

// 1) Baza faylı əvvəldən var idimi? (Backup üçün lazımdır — boş bazanı backup etmək mənasızdır)
var bazaArtiqVarIdi = File.Exists(AppDbContext.BazaFayliYolu);

using var db = new AppDbContext();

// 2) Baza yoxdursa yaradır (fayl + cədvəllər), varsa toxunmur
db.Database.EnsureCreated();

// 3) Backup — yalnız baza əvvəldən mövcud idisə (ilk açılışda backup ediləcək məzmun yoxdur)
if (bazaArtiqVarIdi)
    BackupService.BackupEt(AppDbContext.BazaFayliYolu);

// 4) Baza boşdursa (ilk açılış), test mallarını doldurur
db.IlkVeriləriYarat();

// 5) Mallar artıq kodun içindən yox, bazadan oxunur
var mallar = db.Parts.ToList();

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

// 7) PartService (CRUD) testləri — ƏSL BAZAYA TOXUNMUR, ayrıca müvəqqəti bazada işləyir
Console.WriteLine("\n=== PartService (CRUD) testləri ===\n");

var testBazaYolu = Path.Combine(AppContext.BaseDirectory, "zapcavab_test.db");
if (File.Exists(testBazaYolu))
    File.Delete(testBazaYolu);

var crudHamisiKecdi = true;

void Yoxla(string ad, bool serti)
{
    crudHamisiKecdi &= serti;
    Console.WriteLine($"  {ad}: {(serti ? "✅ PASS" : "❌ FAIL")}");
}

using (var testDb = new AppDbContext(testBazaYolu))
{
    testDb.Database.EnsureCreated();
    var partService = new PartService(testDb);

    Yoxla("Boş bazada Say() == 0", partService.Say() == 0);

    var yeniMal = new Part
    {
        Brand = "Kia", Model = "Rio", YearFrom = 2015, YearTo = 2020,
        PartNameAz = "Ön əyləc altlığı", OemCode = "58101-H9A00", Price = 40, StockQty = 4
    };
    var n1 = partService.Elave(yeniMal);
    Yoxla("Düzgün mal əlavə olunur", n1.Ugurlu && partService.Say() == 1);

    var bosAd = new Part { Brand = "Kia", Model = "Rio", YearFrom = 2015, YearTo = 2020, PartNameAz = "", OemCode = "AAA-111", Price = 10, StockQty = 1 };
    Yoxla("Boş ad rədd olunur", !partService.Elave(bosAd).Ugurlu && partService.Say() == 1);

    var menfiQiymet = new Part { Brand = "Kia", Model = "Rio", YearFrom = 2015, YearTo = 2020, PartNameAz = "Test", OemCode = "AAA-222", Price = -5, StockQty = 1 };
    Yoxla("Mənfi qiymət rədd olunur", !partService.Elave(menfiQiymet).Ugurlu && partService.Say() == 1);

    var menfiQaliq = new Part { Brand = "Kia", Model = "Rio", YearFrom = 2015, YearTo = 2020, PartNameAz = "Test", OemCode = "AAA-333", Price = 10, StockQty = -1 };
    Yoxla("Mənfi qalıq rədd olunur", !partService.Elave(menfiQaliq).Ugurlu && partService.Say() == 1);

    var sehvIl = new Part { Brand = "Kia", Model = "Rio", YearFrom = 2020, YearTo = 2015, PartNameAz = "Test", OemCode = "AAA-444", Price = 10, StockQty = 1 };
    Yoxla("YearFrom > YearTo rədd olunur", !partService.Elave(sehvIl).Ugurlu && partService.Say() == 1);

    var tekrar = new Part { Brand = "Kia", Model = "Sportage", YearFrom = 2018, YearTo = 2022, PartNameAz = "Başqa ad", OemCode = "58101-H9A00", Price = 99, StockQty = 2 };
    Yoxla("Təkrar OEM+marka rədd olunur", !partService.Elave(tekrar).Ugurlu && partService.Say() == 1);

    Yoxla("HamisiniGetir() 1 mal qaytarır", partService.HamisiniGetir().Count == 1);

    var tapilanMal = partService.Getir(yeniMal.Id);
    Yoxla("Getir(mövcud id) malı tapır", tapilanMal != null && tapilanMal.OemCode == "58101-H9A00");

    Yoxla("Getir(mövcud olmayan id) null qaytarır", partService.Getir(9999) == null);

    yeniMal.Price = 55;
    var n7 = partService.Deyis(yeniMal);
    var yenilenmisMal = partService.Getir(yeniMal.Id);
    Yoxla("Deyis() qiyməti yeniləyir", n7.Ugurlu && yenilenmisMal!.Price == 55);

    yeniMal.Price = -100;
    var n8 = partService.Deyis(yeniMal);
    var deyismemisMal = partService.Getir(yeniMal.Id);
    Yoxla("Deyis() mənfi qiyməti rədd edir (dəyişməz qalır)", !n8.Ugurlu && deyismemisMal!.Price == 55);

    var yoxMal = new Part { Id = 9999, Brand = "X", Model = "Y", YearFrom = 2020, YearTo = 2021, PartNameAz = "Test", OemCode = "ZZZ", Price = 1, StockQty = 1 };
    Yoxla("Deyis() mövcud olmayan ID-ni rədd edir", !partService.Deyis(yoxMal).Ugurlu);

    var n10 = partService.Sil(yeniMal.Id);
    Yoxla("Sil() malı silir", n10.Ugurlu && partService.Say() == 0);

    Yoxla("Sil() mövcud olmayan ID-ni rədd edir", !partService.Sil(9999).Ugurlu);
}

// Test bazası yalnız sınaq üçün idi — silirik.
// SQLite bağlantı hovuzu (connection pool) faylı DbContext bağlanandan sonra da açıq
// saxlaya bilər — əvvəlcə hovuzu təmizləyirik ki, "fayl başqa proses tərəfindən
// istifadə olunur" xətası olmasın.
Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
try
{
    File.Delete(testBazaYolu);
}
catch (IOException)
{
    // Test faylı silinə bilmədi — problem deyil, növbəti işə salınmada yenidən
    // silinməyə cəhd olunacaq (bax: faylın yaradılmazdan əvvəlki yoxlama).
}

Console.WriteLine(crudHamisiKecdi ? "\n=== CRUD testləri: hamısı keçdi ✅ ===" : "\n=== CRUD testləri: bəziləri uğursuz oldu ❌ ===");
