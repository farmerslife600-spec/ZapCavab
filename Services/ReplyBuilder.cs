using System.Text;

namespace ZapCavab.Services;

// Axtarış nəticələrindən müştəriyə göndərilə biləcək hazır mətn qurur.
// Sahib bu mətni birbaşa kopyalayıb WhatsApp/Instagram-a yapışdıracaq.
public static class ReplyBuilder
{
    public static string Build(List<SearchResult> neticeler)
    {
        if (neticeler.Count == 0)
        {
            return "Təəssüf ki, uyğun mal tapılmadı. Zəhmət olmasa marka, model, il və hissənin adını yoxlayın.";
        }

        var sb = new StringBuilder();

        // İl uyğun gəlməyən mal varsa, əvvəlcə xəbərdarlıq yazırıq
        if (neticeler.Any(n => n.IlUygunGelmir))
        {
            sb.AppendLine("⚠️ Qeyd: soruşduğunuz ilə tam uyğun mal yoxdur, amma ən yaxın variantlar aşağıdadır:");
            sb.AppendLine();
        }

        var sira = 1;
        foreach (var netice in neticeler)
        {
            var mal = netice.Part;
            var anbarMetni = mal.InStock ? $"anbarda var ({mal.StockQty} ədəd)" : "anbarda yoxdur, sifarişlə gətirilir";

            sb.AppendLine($"{sira}. {mal.Brand} {mal.Model} ({mal.YearFrom}-{mal.YearTo}) — {mal.PartNameAz}");
            sb.AppendLine($"   Qiymət: {mal.Price} AZN | {anbarMetni} | OEM: {mal.OemCode}");
            sira++;
        }

        return sb.ToString().TrimEnd();
    }
}
