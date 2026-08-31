using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using ZapCavab.Helpers;
using ZapCavab.Models;

namespace ZapCavab.Services;

// Bir Excel idxalının nəticəsi: neçə mal əlavə olundu, neçəsi yeniləndi, neçəsi atlandı və niyə.
public class ImportNeticesi
{
    // Fayl ümumiyyətlə oxuna bilmədisə (tapılmadı, başlıqda vacib sütun yoxdur və s.) false olur.
    public bool UmumiUgurlu { get; set; } = true;
    public string? UmumiXeta { get; set; }

    public int EleaveOlunan { get; set; }
    public int Yenilenen { get; set; }
    public int Atlanan { get; set; }

    // Hər atlanan sətir üçün "Sətir N: səbəb" formatında qeyd.
    public List<string> AtlanmaSebebleri { get; set; } = new();
}

// Excel (.xlsx) faylından mal siyahısını bazaya idxal edir.
// Eyni Marka+OEM kodlu mal artıq varsa YENİLƏNİR (qiymət, qalıq) — yenisi əlavə olunmur,
// ona görə eyni faylı 2-ci dəfə yükləmək təkrar mal yaratmır.
public class ExcelImportService
{
    private readonly PartService _partService;

    // Sütun başlıqlarının tanına biləcək bütün formaları (TextHelper.Normalize-dən keçmiş formada).
    // "buna hazır ol": Ad/Adı/Name/Mal/Məhsul kimi fərqli yazılışlar hamısı eyni sahəyə düşür.
    private static readonly Dictionary<string, string[]> BasliqSinonimleri = new()
    {
        ["ad"] = new[] { "ad", "adi", "name", "mal", "mehsul" },
        ["oem"] = new[] { "oem", "oem kod", "oem kodu", "kod" },
        ["brand"] = new[] { "brend", "marka", "brand" },
        ["model"] = new[] { "model" },
        ["ilbaslangic"] = new[] { "il baslangic", "ilbaslangic", "baslangic il", "il1" },
        ["ilson"] = new[] { "il son", "ilson", "son il", "il2" },
        ["qaliq"] = new[] { "qaliq", "anbar", "say", "miqdar", "stok" },
        ["qiymet"] = new[] { "qiymet", "qiymeti", "price" },
        ["raf"] = new[] { "raf" },
    };

    // İdxal üçün mütləq lazım olan sahələr — bunlar başlıqda tapılmasa, idxal ümumiyyətlə başlamır.
    private static readonly string[] VacibSahalar = { "ad", "brand", "oem" };

    public ExcelImportService(PartService partService)
    {
        _partService = partService;
    }

    // Faylı oxuyub bazaya idxal edir.
    public ImportNeticesi Idxal(string faylYolu)
    {
        var netice = new ImportNeticesi();

        if (!File.Exists(faylYolu))
        {
            netice.UmumiUgurlu = false;
            netice.UmumiXeta = $"Fayl tapılmadı: {faylYolu}";
            return netice;
        }

        using var kitab = new XLWorkbook(faylYolu);
        var vereqe = kitab.Worksheets.First();

        var basliqSetri = vereqe.Row(1);
        var sutunXeritesi = SutunlariTani(basliqSetri);

        var catmayanSahalar = VacibSahalar.Where(s => !sutunXeritesi.ContainsKey(s)).ToList();
        if (catmayanSahalar.Count > 0)
        {
            netice.UmumiUgurlu = false;
            netice.UmumiXeta = $"Başlıq sətrində tələb olunan sütun(lar) tapılmadı: {string.Join(", ", catmayanSahalar)}";
            return netice;
        }

        var sonSetirNo = vereqe.LastRowUsed()?.RowNumber() ?? 1;

        for (var setirNo = 2; setirNo <= sonSetirNo; setirNo++)
        {
            var setir = vereqe.Row(setirNo);

            if (!setir.CellsUsed().Any())
                continue; // tamamilə boş sətir — sükutla keç

            var (mal, xeta) = SetirdenMalYarat(setir, sutunXeritesi);
            if (xeta != null)
            {
                netice.Atlanan++;
                netice.AtlanmaSebebleri.Add($"Sətir {setirNo}: {xeta}");
                continue;
            }

            var movcud = _partService.BrandVeOemIleTap(mal!.Brand, mal.OemCode);

            if (movcud != null)
            {
                // Mal artıq var — yalnız qiymət və qalığı yeniləyirik (tələb belədir)
                movcud.Price = mal.Price;
                movcud.StockQty = mal.StockQty;

                var yenilemeNeticesi = _partService.Deyis(movcud);
                if (yenilemeNeticesi.Ugurlu)
                    netice.Yenilenen++;
                else
                {
                    netice.Atlanan++;
                    netice.AtlanmaSebebleri.Add($"Sətir {setirNo}: {yenilemeNeticesi.Mesaj}");
                }
            }
            else
            {
                var elaveNeticesi = _partService.Elave(mal);
                if (elaveNeticesi.Ugurlu)
                    netice.EleaveOlunan++;
                else
                {
                    netice.Atlanan++;
                    netice.AtlanmaSebebleri.Add($"Sətir {setirNo}: {elaveNeticesi.Mesaj}");
                }
            }
        }

        return netice;
    }

    // Başlıq sətrindəki hər xananı tanıyıb "hansı sütun nömrəsi hansı sahəyə aiddir" xəritəsi qurur.
    private static Dictionary<string, int> SutunlariTani(IXLRow basliqSetri)
    {
        var xerite = new Dictionary<string, int>();
        var sonSutunNo = basliqSetri.LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var sutunNo = 1; sutunNo <= sonSutunNo; sutunNo++)
        {
            var xamMetn = basliqSetri.Cell(sutunNo).GetString();
            if (string.IsNullOrWhiteSpace(xamMetn))
                continue;

            var normallasmis = TextHelper.Normalize(xamMetn);

            foreach (var (sahaAdi, sinonimler) in BasliqSinonimleri)
            {
                if (!xerite.ContainsKey(sahaAdi) && sinonimler.Contains(normallasmis))
                {
                    xerite[sahaAdi] = sutunNo;
                    break;
                }
            }
        }

        return xerite;
    }

    // Bir sətri Part obyektinə çevirir. Uğursuz olarsa Mal null olur, Xeta səbəbi göstərir.
    private static (Part? Mal, string? Xeta) SetirdenMalYarat(IXLRow setir, Dictionary<string, int> sutunXeritesi)
    {
        string MetinOxu(string sahaAdi) =>
            sutunXeritesi.TryGetValue(sahaAdi, out var sutun) ? setir.Cell(sutun).GetString().Trim() : string.Empty;

        var ad = MetinOxu("ad");
        var oem = MetinOxu("oem");
        var brand = MetinOxu("brand");

        if (string.IsNullOrWhiteSpace(ad))
            return (null, "malın adı boşdur");

        if (string.IsNullOrWhiteSpace(brand))
            return (null, "marka/brend boşdur");

        if (string.IsNullOrWhiteSpace(oem))
            return (null, "OEM kod boşdur");

        if (!TamEdedOxu(setir, sutunXeritesi, "ilbaslangic", 0, out var ilBaslangic))
            return (null, "'il başlanğıc' rəqəm deyil");

        if (!TamEdedOxu(setir, sutunXeritesi, "ilson", 0, out var ilSon))
            return (null, "'il son' rəqəm deyil");

        if (!TamEdedOxu(setir, sutunXeritesi, "qaliq", 0, out var qaliq))
            return (null, "'qalıq' rəqəm deyil");

        if (!OnluluEdedOxu(setir, sutunXeritesi, "qiymet", 0, out var qiymet))
            return (null, "'qiymət' rəqəm deyil");

        var mal = new Part
        {
            PartNameAz = ad,
            OemCode = oem,
            Brand = brand,
            Model = MetinOxu("model"),
            YearFrom = ilBaslangic,
            YearTo = ilSon,
            StockQty = qaliq,
            Price = qiymet,
            RafYeri = MetinOxu("raf")
        };

        return (mal, null);
    }

    // Bir xanadan tam ədəd oxumağa çalışır. Sütun ümumiyyətlə yoxdursa və ya xana boşdursa,
    // defolt dəyəri qəbul edir (uğurlu sayılır). Yalnız DOLU AMMA ƏDƏD OLMAYAN xana üçün false qaytarır.
    private static bool TamEdedOxu(IXLRow setir, Dictionary<string, int> xerite, string sahaAdi, int defolt, out int netice)
    {
        netice = defolt;
        if (!xerite.TryGetValue(sahaAdi, out var sutun))
            return true;

        var metin = setir.Cell(sutun).GetString().Trim();
        if (string.IsNullOrEmpty(metin))
            return true;

        // Excel bəzən "2015.0" kimi kəsr formatında saxlaya bilər — əvvəlcə kəsr kimi oxuyub yuvarlaqlaşdırırıq
        if (double.TryParse(metin, NumberStyles.Any, CultureInfo.InvariantCulture, out var kesrDeyer))
        {
            netice = (int)kesrDeyer;
            return true;
        }

        return false;
    }

    // Eyni məntiq, amma qiymət kimi kəsr (onluq) dəyərlər üçün.
    private static bool OnluluEdedOxu(IXLRow setir, Dictionary<string, int> xerite, string sahaAdi, decimal defolt, out decimal netice)
    {
        netice = defolt;
        if (!xerite.TryGetValue(sahaAdi, out var sutun))
            return true;

        var metin = setir.Cell(sutun).GetString().Trim();
        if (string.IsNullOrEmpty(metin))
            return true;

        return decimal.TryParse(metin, NumberStyles.Any, CultureInfo.InvariantCulture, out netice);
    }
}
