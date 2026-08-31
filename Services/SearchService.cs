using ZapCavab.Data;
using ZapCavab.Helpers;
using ZapCavab.Models;

namespace ZapCavab.Services;

// Bir axtarış nəticəsini (mal + uyğunluq balı + il xəbərdarlığı) saxlayır.
public class SearchResult
{
    public Part Part { get; set; } = null!;
    public double Score { get; set; }
    public bool IlUygunGelmir { get; set; }
}

// CLAUDE.md-dəki axtarış alqoritmini həyata keçirir.
// İndiki versiya sorğunu sözlərə bölüb hər sözü NÖVünə görə təsnif edir:
// MARKA/MODEL, HİSSƏ ADI, MÖVQE (ön/arxa/sağ/sol), İL, TANINMAYAN.
public static class SearchService
{
    // Balı bu həddən aşağı (daxil olmaqla) olan mallar göstərilmir.
    private const double MinBal = 0.5;

    // İl uyğun gəlmirsə baldan çıxılan cəza.
    private const double IlCezasi = 0.35;

    // Mövqe (ön/arxa/sağ/sol) uyğun gəlmirsə baldan çıxılan cəza.
    private const double MovqeCezasi = 0.4;

    // Mövqe sözlərinin qapalı siyahısı — bunlar hər mağazada eynidir, kataloqdan asılı deyil.
    private static readonly HashSet<string> MovqeSozleri = new() { "on", "arxa", "sag", "sol" };

    public static List<SearchResult> Search(string sorgu, IEnumerable<Part>? mallar)
    {
        var neticeler = new List<SearchResult>();

        // Mal siyahısı olmadan axtarış aparıla bilməz — çökmək əvəzinə boş nəticə qaytarırıq.
        if (mallar == null)
            return neticeler;

        var mallarSiyahisi = mallar.ToList();

        // 1-3. Normalizasiya + sinonim + il çıxarma
        var normalize = TextHelper.Normalize(sorgu);
        var kanonik = SynonymDictionary.ToCanonical(normalize);
        var il = TextHelper.ExtractYear(kanonik);
        var ilsizMetn = TextHelper.RemoveYear(kanonik);

        // 4. Sözlərə böl, stop-word-ləri at
        var xamSozler = TextHelper.SplitWords(ilsizMetn);
        var sorguSozleri = StopWords.Cixar(xamSozler);

        // Heç bir söz və il qalmayıbsa (məs. "salam, necəsən?"), axtaracaq heç nə yoxdur
        if (sorguSozleri.Count == 0 && !il.HasValue)
            return neticeler;

        // Kataloqdan marka/model və hissə adı lüğətini qururuq (statik siyahı yox,
        // dükanda həqiqətən olan mallardan çıxarılır)
        var (markaModelVokab, hisseAdiVokab) = VokabulyariyaQur(mallarSiyahisi);

        // Hər sözü növünə görə təsnif et
        var sozNovleri = sorguSozleri
            .Select(s => (Soz: s, Nov: SozNoveAyir(s, markaModelVokab, hisseAdiVokab)))
            .ToList();

        var markaModelTelebleri = sozNovleri.Where(t => t.Nov == SozNovu.MarkaModel).Select(t => t.Soz).ToList();
        var hisseAdiTelebleri = sozNovleri.Where(t => t.Nov == SozNovu.HisseAdi).Select(t => t.Soz).ToList();
        var movqeTelebleri = sozNovleri.Where(t => t.Nov == SozNovu.Movqe).Select(t => t.Soz).ToList();

        // QAYDA: sorğuda HEÇ BİR HİSSƏ ADI tanınmayıbsa (yalnız marka/model və ya il varsa),
        // heç nə göstərmə. Səbəb: "hansı hissəni axtarır" bilinmədən mal göstərmək YANLIŞ
        // cavab riski yaradır — yanlış cavab boş cavabdan pisdir (məs. "Prado 2015 turbo"
        // sorğusuna kolodka göstərmək müştərini çaşdırıb itirə bilər).
        if (hisseAdiTelebleri.Count == 0)
            return neticeler;

        foreach (var mal in mallarSiyahisi)
        {
            var malXamMetn = $"{mal.Brand} {mal.Model} {mal.PartNameAz} {mal.OemCode}";
            var malKanonik = SynonymDictionary.ToCanonical(TextHelper.Normalize(malXamMetn));
            var malSozleri = TextHelper.SplitWords(malKanonik);

            // Qayda: sorğuda MARKA/MODEL sözü var, mal ona uyğun deyil -> göstərmə
            var markaUygunGelmir = markaModelTelebleri
                .Any(teleb => !malSozleri.Any(m => TextHelper.KokUygunGelir(m, teleb)));
            if (markaUygunGelmir)
                continue;

            // Qayda: sorğuda HİSSƏ ADI sözü var, mal ona uyğun deyil -> göstərmə
            var hisseUygunGelmir = hisseAdiTelebleri
                .Any(teleb => !malSozleri.Any(m => TextHelper.KokUygunGelir(m, teleb)));
            if (hisseUygunGelmir)
                continue;

            var bal = 1.0;

            // Qayda: MÖVQE uyğun gəlmirsə 0.4 cəza (amma tam atma)
            foreach (var m in movqeTelebleri)
            {
                if (!malSozleri.Contains(m))
                    bal -= MovqeCezasi;
            }

            // İl uyğun gəlmirsə cəza (amma tam atma)
            var ilUygunGelmir = false;
            if (il.HasValue && (il.Value < mal.YearFrom || il.Value > mal.YearTo))
            {
                bal -= IlCezasi;
                ilUygunGelmir = true;
            }

            if (bal <= MinBal)
                continue;

            neticeler.Add(new SearchResult
            {
                Part = mal,
                Score = bal,
                IlUygunGelmir = ilUygunGelmir
            });
        }

        // Sırala: bal -> anbarda var -> ucuz
        return neticeler
            .OrderByDescending(n => n.Score)
            .ThenByDescending(n => n.Part.InStock)
            .ThenBy(n => n.Part.Price)
            .ToList();
    }

    private enum SozNovu
    {
        MarkaModel,
        HisseAdi,
        Movqe,
        Taninmayan
    }

    // Bir sözün hansı növə aid olduğunu tapır.
    private static SozNovu SozNoveAyir(string soz, HashSet<string> markaModelVokab, HashSet<string> hisseAdiVokab)
    {
        if (MovqeSozleri.Contains(soz))
            return SozNovu.Movqe;

        if (markaModelVokab.Any(v => TextHelper.KokUygunGelir(v, soz)))
            return SozNovu.MarkaModel;

        if (hisseAdiVokab.Any(v => TextHelper.KokUygunGelir(v, soz)))
            return SozNovu.HisseAdi;

        return SozNovu.Taninmayan;
    }

    // Kataloqdakı bütün malların Brand/Model sözlərindən "marka/model lüğəti",
    // PartNameAz sözlərindən isə "hissə adı lüğəti" qurur. Bu lüğətlər statik deyil —
    // dükanda həqiqətən olan mallara görə avtomatik dəyişir.
    private static (HashSet<string> MarkaModel, HashSet<string> HisseAdi) VokabulyariyaQur(List<Part> mallar)
    {
        var markaModel = new HashSet<string>();
        var hisseAdi = new HashSet<string>();

        foreach (var mal in mallar)
        {
            var markaModelMetn = SynonymDictionary.ToCanonical(TextHelper.Normalize($"{mal.Brand} {mal.Model}"));
            foreach (var soz in TextHelper.SplitWords(markaModelMetn))
                markaModel.Add(soz);

            var hisseMetn = SynonymDictionary.ToCanonical(TextHelper.Normalize(mal.PartNameAz));
            foreach (var soz in TextHelper.SplitWords(hisseMetn))
            {
                if (!MovqeSozleri.Contains(soz))
                    hisseAdi.Add(soz);
            }
        }

        // Ehtiyat tədbiri: hissə adı lüğətindən marka/model sözlərini çıxarırıq
        hisseAdi.ExceptWith(markaModel);

        return (markaModel, hisseAdi);
    }
}
