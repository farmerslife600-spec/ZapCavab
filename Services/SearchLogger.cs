using System.Globalization;
using System.Text;

namespace ZapCavab.Services;

// Bir axtarış qeydi: nə axtarılıb, neçə nəticə tapılıb, nə vaxt.
public class SearchLogEntry
{
    public string Sorgu { get; set; } = string.Empty;
    public int NeticeSayi { get; set; }
    public DateTime Tarix { get; set; }
}

// Hər axtarışı həm yaddaşda saxlayır, həm də "axtaris_jurnali.csv" faylına yazır ki,
// proqram bağlanandan sonra da axtarış tarixçəsi itməsin (V1 tələbi: axtarış statistikası).
public class SearchLogger
{
    private readonly List<SearchLogEntry> _qeydler = new();
    private readonly string _faylYolu;

    public SearchLogger(string? faylYolu = null)
    {
        _faylYolu = faylYolu ?? Path.Combine(AppContext.BaseDirectory, "axtaris_jurnali.csv");
        FayldanYukle();
    }

    // Axtarışı jurnala yazır — həm yaddaşa, həm də fayla (dərhal, gözləmədən).
    public void Log(string sorgu, int neticeSayi)
    {
        var qeyd = new SearchLogEntry
        {
            Sorgu = sorgu,
            NeticeSayi = neticeSayi,
            Tarix = DateTime.Now
        };

        _qeydler.Add(qeyd);
        FaylaElaveEt(qeyd);
    }

    // Heç nəticə verməyən axtarışları qaytarır (sahib bunları görüb mal əlavə edə bilər).
    public List<string> TapilmayanlariGetir()
    {
        return _qeydler
            .Where(q => q.NeticeSayi == 0)
            .Select(q => q.Sorgu)
            .Distinct()
            .ToList();
    }

    // Ən çox təkrarlanan axtarışları qaytarır (böyükdən kiçiyə sıralanmış).
    public List<(string Sorgu, int Say)> EnCoxAxtarilanlariGetir(int say = 5)
    {
        return _qeydler
            .GroupBy(q => q.Sorgu)
            .Select(g => (Sorgu: g.Key, Say: g.Count()))
            .OrderByDescending(x => x.Say)
            .Take(say)
            .ToList();
    }

    // Bir qeydi CSV sətri kimi fayla əlavə edir. Fayl yoxdursa, əvvəlcə başlıq sətri yazılır.
    private void FaylaElaveEt(SearchLogEntry qeyd)
    {
        var yeniFayldir = !File.Exists(_faylYolu);
        using var yazici = new StreamWriter(_faylYolu, append: true, Encoding.UTF8);

        if (yeniFayldir)
            yazici.WriteLine("Tarix,Axtaris,NeticeSayi,Tapildi");

        var tapildi = qeyd.NeticeSayi > 0 ? "beli" : "xeyr";
        yazici.WriteLine($"{qeyd.Tarix:yyyy-MM-dd HH:mm:ss},{CsvUcunHazirla(qeyd.Sorgu)},{qeyd.NeticeSayi},{tapildi}");
    }

    // Proqram yenidən açılanda köhnə jurnalı fayldan yaddaşa yükləyir ki,
    // statistika (ən çox axtarılan, tapılmayan) bütün tarixçəni əhatə etsin, təkcə bu sessiyanı yox.
    private void FayldanYukle()
    {
        if (!File.Exists(_faylYolu))
            return;

        var setirler = File.ReadAllLines(_faylYolu, Encoding.UTF8);

        foreach (var setir in setirler.Skip(1)) // başlıq sətrini ötür
        {
            var hisseler = CsvSetriniAyir(setir);
            if (hisseler.Count != 4)
                continue;

            if (!DateTime.TryParse(hisseler[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var tarix))
                continue;
            if (!int.TryParse(hisseler[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var neticeSayi))
                continue;

            _qeydler.Add(new SearchLogEntry
            {
                Tarix = tarix,
                Sorgu = hisseler[1],
                NeticeSayi = neticeSayi
            });
        }
    }

    // Axtarış mətnində vergül/dırnaq ola bilər deyə, CSV qaydasına uyğun dırnaqlayır.
    private static string CsvUcunHazirla(string metn)
    {
        var qacirilmis = metn.Replace("\"", "\"\"");
        return $"\"{qacirilmis}\"";
    }

    // Dırnaqlanmış bir CSV sətrini sahələrə ayırır (dırnaq içindəki vergüllər nəzərə alınmır).
    private static List<string> CsvSetriniAyir(string setir)
    {
        var neticeler = new List<string>();
        var cari = new StringBuilder();
        var dirnaqIcinde = false;

        for (var i = 0; i < setir.Length; i++)
        {
            var simvol = setir[i];

            if (simvol == '"')
            {
                if (dirnaqIcinde && i + 1 < setir.Length && setir[i + 1] == '"')
                {
                    cari.Append('"');
                    i++;
                }
                else
                {
                    dirnaqIcinde = !dirnaqIcinde;
                }
            }
            else if (simvol == ',' && !dirnaqIcinde)
            {
                neticeler.Add(cari.ToString());
                cari.Clear();
            }
            else
            {
                cari.Append(simvol);
            }
        }

        neticeler.Add(cari.ToString());
        return neticeler;
    }
}
