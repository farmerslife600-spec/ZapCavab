namespace ZapCavab.Models;

// Hazır cavab mətni qəlibi — sahib bunu Ayarlar bölməsində özü redaktə edə biləcək.
public class Template
{
    public int Id { get; set; }

    // Şablonun adı (məs. "Standart cavab", "Anbarda yoxdur")
    public string Ad { get; set; } = string.Empty;

    // Şablonun özü — ReplyBuilder bunu istifadə edəcək
    public string Metn { get; set; } = string.Empty;
}
