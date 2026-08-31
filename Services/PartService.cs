using ZapCavab.Data;
using ZapCavab.Models;

namespace ZapCavab.Services;

// Bir metodun (Elave, Deyis, Sil) nəticəsini bildirir — uğurlu oldumu, olmadısa niyə.
public class NeticeMesaji
{
    public bool Ugurlu { get; set; }
    public string Mesaj { get; set; } = string.Empty;
}

// Mal əlavə/dəyiş/sil/oxu əməliyyatlarını yerinə yetirir. Pəncərə (WPF) hələ yoxdur —
// bu, sadəcə arxa fon metodlarıdır.
public class PartService
{
    private readonly AppDbContext _db;

    public PartService(AppDbContext db)
    {
        _db = db;
    }

    // Bütün malları qaytarır.
    public List<Part> HamisiniGetir()
    {
        return _db.Parts.ToList();
    }

    // Bir malı ID-yə görə qaytarır. Tapılmasa null qaytarır (çökmür).
    public Part? Getir(int id)
    {
        return _db.Parts.Find(id);
    }

    // Anbardakı mal sayını qaytarır.
    public int Say()
    {
        return _db.Parts.Count();
    }

    // Marka + OEM kod üzrə mövcud malı tapır (böyük/kiçik hərfə həssas deyil).
    // Excel idxalında "bu mal artıq bazadadırmı?" sualına cavab vermək üçün istifadə olunur.
    public Part? BrandVeOemIleTap(string brand, string oemCode)
    {
        return _db.Parts.FirstOrDefault(p =>
            p.Brand.ToLower() == brand.ToLower() &&
            p.OemCode.ToLower() == oemCode.ToLower());
    }

    // Yeni mal əlavə edir. Uyğunsuzluq varsa əlavə etmir, səbəbini mesajda qaytarır.
    public NeticeMesaji Elave(Part mal)
    {
        try
        {
            var xeta = Yoxla(mal, istisnaId: null);
            if (xeta != null)
                return new NeticeMesaji { Ugurlu = false, Mesaj = xeta };

            _db.Parts.Add(mal);
            _db.SaveChanges();
            return new NeticeMesaji { Ugurlu = true, Mesaj = "Mal əlavə olundu." };
        }
        catch (Exception ex)
        {
            return new NeticeMesaji { Ugurlu = false, Mesaj = $"Gözlənilməz xəta: {ex.Message}" };
        }
    }

    // Mövcud malı yeniləyir (mal.Id ilə tapılır). Uyğunsuzluq varsa dəyişdirmir.
    public NeticeMesaji Deyis(Part mal)
    {
        try
        {
            var movcud = _db.Parts.Find(mal.Id);
            if (movcud == null)
                return new NeticeMesaji { Ugurlu = false, Mesaj = $"ID={mal.Id} olan mal tapılmadı." };

            var xeta = Yoxla(mal, istisnaId: mal.Id);
            if (xeta != null)
            {
                // DİQQƏT: əgər çağıran "mal" olaraq elə "movcud"un özünü göndəribsə (məs. WPF
                // formasında eyni obyekt redaktə olunubsa), onun sahələri artıq yaddaşda
                // (səhv qiymətlə) dəyişmiş ola bilər — hələ saxlanmayıb, amma "çirklənib".
                // Reload() bazadakı son doğru vəziyyəti geri yükləyir ki, bu çirkli məlumat
                // təsadüfən başqa yerdən saxlanmasın.
                _db.Entry(movcud).Reload();
                return new NeticeMesaji { Ugurlu = false, Mesaj = xeta };
            }

            movcud.Brand = mal.Brand;
            movcud.Model = mal.Model;
            movcud.YearFrom = mal.YearFrom;
            movcud.YearTo = mal.YearTo;
            movcud.PartNameAz = mal.PartNameAz;
            movcud.OemCode = mal.OemCode;
            movcud.Price = mal.Price;
            movcud.StockQty = mal.StockQty;

            _db.SaveChanges();
            return new NeticeMesaji { Ugurlu = true, Mesaj = "Mal yeniləndi." };
        }
        catch (Exception ex)
        {
            return new NeticeMesaji { Ugurlu = false, Mesaj = $"Gözlənilməz xəta: {ex.Message}" };
        }
    }

    // Malı ID-yə görə silir.
    public NeticeMesaji Sil(int id)
    {
        try
        {
            var mal = _db.Parts.Find(id);
            if (mal == null)
                return new NeticeMesaji { Ugurlu = false, Mesaj = $"ID={id} olan mal tapılmadı." };

            _db.Parts.Remove(mal);
            _db.SaveChanges();
            return new NeticeMesaji { Ugurlu = true, Mesaj = "Mal silindi." };
        }
        catch (Exception ex)
        {
            return new NeticeMesaji { Ugurlu = false, Mesaj = $"Gözlənilməz xəta: {ex.Message}" };
        }
    }

    // Bir malın düzgün olub-olmadığını yoxlayır. Hər şey qaydasındadırsa null qaytarır,
    // problemi varsa səbəbini yazır. "istisnaId" — Deyis zamanı malın öz-özü ilə
    // "təkrardır" deyə qarışdırılmaması üçündür (Elave zamanı null verilir).
    private string? Yoxla(Part mal, int? istisnaId)
    {
        if (string.IsNullOrWhiteSpace(mal.PartNameAz))
            return "Malın adı boş ola bilməz.";

        if (mal.Price < 0)
            return "Qiymət mənfi ola bilməz.";

        if (mal.StockQty < 0)
            return "Qalıq (anbar sayı) mənfi ola bilməz.";

        if (mal.YearFrom > mal.YearTo)
            return "Başlanğıc il (YearFrom) bitiş ildən (YearTo) böyük ola bilməz.";

        var tekrarVarmi = _db.Parts.Any(p =>
            p.Id != istisnaId &&
            p.Brand.ToLower() == mal.Brand.ToLower() &&
            p.OemCode.ToLower() == mal.OemCode.ToLower());

        if (tekrarVarmi)
            return $"'{mal.Brand}' markası üçün '{mal.OemCode}' OEM kodu artıq mövcuddur.";

        return null;
    }
}
