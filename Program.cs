using ClosedXML.Excel;
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

// 8) Excel idxal testi — nümunə fayl yaradıb ƏSL bazaya idxal edirik (2 dəfə, təkrarlanma olmasın deyə)
Console.WriteLine("\n=== Excel idxal testi ===\n");

var numuneFayl = Path.Combine(AppContext.BaseDirectory, "numune_mallar.xlsx");
NumuneExcelYarat(numuneFayl);

var importServisi = new ExcelImportService(new PartService(db));

Console.WriteLine($"Bazada idxaldan ƏVVƏL: {db.Parts.Count()} mal\n");

Console.WriteLine("--- 1-ci idxal (fayl ilk dəfə yüklənir) ---");
NeticeCapEt(importServisi.Idxal(numuneFayl));

Console.WriteLine("\n--- 2-ci idxal (EYNİ fayl TƏKRAR yüklənir — heç nə təkrarlanmamalıdır) ---");
NeticeCapEt(importServisi.Idxal(numuneFayl));

Console.WriteLine($"\nBazada idxaldan SONRA: {db.Parts.Count()} mal");

void NeticeCapEt(ImportNeticesi n)
{
    if (!n.UmumiUgurlu)
    {
        Console.WriteLine($"  ÜMUMİ XƏTA: {n.UmumiXeta}");
        return;
    }

    Console.WriteLine($"  Əlavə olundu: {n.EleaveOlunan}");
    Console.WriteLine($"  Yeniləndi: {n.Yenilenen}");
    Console.WriteLine($"  Atlandı: {n.Atlanan}");

    if (n.AtlanmaSebebleri.Count > 0)
    {
        Console.WriteLine("  Atlanma səbəbləri:");
        foreach (var s in n.AtlanmaSebebleri)
            Console.WriteLine($"    - {s}");
    }
}

// Test üçün nümunə .xlsx faylı yaradır: 20 sətir (17 düzgün + 3 səhv), sütun adları
// qəsdən qarışıq yazılıb (Adı/Marka/OEM Kodu/Say) ki, avtomatik tanıma yoxlanılsın.
// 2 sətir mövcud seed mallarla eyni Marka+OEM daşıyır (YENİLƏNMƏ yolunu sınamaq üçün).
void NumuneExcelYarat(string yol)
{
    using var kitab = new XLWorkbook();
    var vereqe = kitab.Worksheets.Add("Mallar");

    var basliqlar = new[] { "Adı", "OEM Kodu", "Marka", "Model", "İl başlanğıc", "İl son", "Say", "Qiymət", "Rəf" };
    for (var i = 0; i < basliqlar.Length; i++)
        vereqe.Cell(1, i + 1).Value = basliqlar[i];

    object?[][] setirler =
    {
        // Mövcud seed mallarla eyni Marka+OEM — YENİLƏNMƏ testi (qiymət/qalıq dəyişib)
        new object?[] { "Ön əyləc altlığı", "04465-60310", "Toyota", "Prado", 2010, 2017, 8, 50, "A1" },
        new object?[] { "Yağ filtri", "90915-YZZD4", "Toyota", "Camry", 2015, 2019, 20, 15, "B2" },

        // Yeni mallar — ƏLAVƏ ETMƏ testi
        new object?[] { "Arxa əyləc altlığı", "04466-60320", "Toyota", "Prado", 2010, 2017, 6, 48, "A2" },
        new object?[] { "Hava filtri", "17801-0V010", "Toyota", "Camry", 2015, 2019, 15, 18, "B3" },
        new object?[] { "Salon filtri", "87139-0N030", "Toyota", "Camry", 2015, 2019, 10, 22, "B4" },
        new object?[] { "Şam", "90919-01247", "Toyota", "Camry", 2015, 2019, 30, 8, "C1" },
        new object?[] { "Amortizator", "KYB334523", "Hyundai", "Elantra", 2016, 2020, 4, 150, "D1" },
        new object?[] { "Radiator", "25310-2E200", "Hyundai", "Elantra", 2016, 2020, 2, 280, "D2" },
        new object?[] { "Akkumulyator", "56-19", "Kia", "Rio", 2015, 2020, 5, 180, "E1" },
        new object?[] { "Əyləc diski", "517123", "Kia", "Sportage", 2016, 2021, 6, 90, "E2" },
        new object?[] { "Kəmər", "13568-0T010", "Toyota", "Prado", 2010, 2017, 3, 65, "A3" },
        new object?[] { "Reyka", "45510-60271", "Toyota", "Prado", 2010, 2017, 1, 420, "A4" },
        new object?[] { "Bufer", "52119-60957", "Toyota", "Prado", 2010, 2017, 1, 350, "A5" },
        new object?[] { "Güzgü", "87910-60492", "Toyota", "Prado", 2010, 2017, 2, 95, "A6" },
        new object?[] { "Generator", "27060-31170", "Toyota", "Camry", 2015, 2019, 2, 320, "B5" },
        new object?[] { "Starter", "28100-31170", "Toyota", "Camry", 2015, 2019, 2, 290, "B6" },
        new object?[] { "Topça", "43330-09030", "Toyota", "Camry", 2015, 2019, 4, 75, "B7" },

        // 3 SƏHV SƏTIR
        new object?[] { "", "12345-ABCDE", "Toyota", "Prado", 2010, 2017, 5, 40, "F1" },       // boş ad
        new object?[] { "Tyaqa", "45450-09010", "Toyota", "Prado", 2010, 2017, 3, -25, "F2" },  // mənfi qiymət
        new object?[] { "Şlanq", "16571-0V010", "Toyota", "Camry", 2019, 2015, 5, 20, "F3" },   // il başlanğıc > il son
    };

    for (var setirIndeksi = 0; setirIndeksi < setirler.Length; setirIndeksi++)
    {
        for (var sutunIndeksi = 0; sutunIndeksi < setirler[setirIndeksi].Length; sutunIndeksi++)
        {
            var deyer = setirler[setirIndeksi][sutunIndeksi];
            var xana = vereqe.Cell(setirIndeksi + 2, sutunIndeksi + 1);

            if (deyer is int tamEded)
                xana.Value = tamEded;
            else
                xana.Value = deyer?.ToString() ?? string.Empty;
        }
    }

    kitab.SaveAs(yol);
}
