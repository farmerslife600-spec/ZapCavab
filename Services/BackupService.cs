using System.Globalization;
using System.IO;

namespace ZapCavab.Services;

// Proqram hər açılanda baza faylının kopyasını backup/ qovluğuna atır.
// Son 7 günün backup-ı saxlanılır, köhnələr avtomatik silinir (V1 kritik tələbi).
public static class BackupService
{
    private const int SaxlanacaqGunSayi = 7;

    // Bir günlük tarix formatı — eyni gün içində bir neçə dəfə açılsa, üzərinə yazılır
    // (günə bir backup kifayətdir, hər açılışda ayrıca fayl yaratmağa ehtiyac yoxdur).
    private const string TarixFormati = "yyyy-MM-dd";

    // Baza faylının kopyasını backup/ qovluğuna atır və köhnə backup-ları təmizləyir.
    public static void BackupEt(string bazaFayliYolu)
    {
        if (!File.Exists(bazaFayliYolu))
            return; // hələ baza yaranmayıbsa, kopyalayacaq heç nə yoxdur

        var proqramQovlugu = Path.GetDirectoryName(bazaFayliYolu) ?? AppContext.BaseDirectory;
        var backupQovlugu = Path.Combine(proqramQovlugu, "backup");
        Directory.CreateDirectory(backupQovlugu);

        var buGununAdi = $"zapcavab_{DateTime.Now.ToString(TarixFormati, CultureInfo.InvariantCulture)}.db";
        var hedefYol = Path.Combine(backupQovlugu, buGununAdi);

        File.Copy(bazaFayliYolu, hedefYol, overwrite: true);

        KohneBackuplariSil(backupQovlugu);
    }

    // 7 gündən köhnə backup fayllarını silir. Tarixi fayl adından oxuyur
    // (fayl sistemi tarixindən asılı olmamaq üçün — daha etibarlıdır).
    private static void KohneBackuplariSil(string backupQovlugu)
    {
        var hedd = DateTime.Now.Date.AddDays(-SaxlanacaqGunSayi);

        foreach (var fayl in Directory.GetFiles(backupQovlugu, "zapcavab_*.db"))
        {
            var tarixHissesi = Path.GetFileNameWithoutExtension(fayl).Replace("zapcavab_", "");

            if (!DateTime.TryParseExact(tarixHissesi, TarixFormati, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tarix))
                continue; // gözlənilməyən adlı fayla toxunma

            if (tarix < hedd)
                File.Delete(fayl);
        }
    }
}
