# ZapCavab — Layihə Təlimatı

Bu fayl Claude Code üçündür. Layihə qovluğunun kökündə saxla.

---

## Layihə nədir

Zapçast (avtomobil ehtiyat hissələri) dükanı üçün **cavab köməkçisi**.

Dükan sahibi WhatsApp/Instagram-da gündə onlarla eyni sualı alır:
"Prado 2015 ön kolodka varmı, neçəyə?"

Proqram: axtarışı qəbul edir → malı tapır → **hazır cavab mətnini** qurur → panoya kopyalayır.
Sahib onu WhatsApp-a yapışdırıb göndərir.

---

## Texniki qərarlar (dəyişmə)

| Sahə | Seçim |
|---|---|
| Framework | .NET 8 (LTS) |
| UI | WPF |
| Baza | SQLite |
| ORM | Entity Framework Core |
| Memarlıq | **Code-behind. MVVM İSTİFADƏ ETMƏ.** |
| Excel | ClosedXML |
| Dil | Kod ingiliscə, şərhlər Azərbaycan dilində |

---

## MÜTLƏQ QAYDALAR

1. **MVVM, DI konteyner, Repository pattern istifadə etmə.** Sahibi yeni başlayandır — sadə code-behind yaz.
2. **Hər metodun üstünə Azərbaycan dilində qısa şərh yaz** — nə edir, niyə lazımdır.
3. **Yeni NuGet paketi əlavə etməzdən əvvəl soruş.** Az asılılıq = az problem.
4. **Bir dəfəyə bir funksiya.** Sahib hər addımı başa düşməlidir.
5. **Kod yazandan sonra izah et** — hansı faylı niyə dəyişdin.
6. **Sadə həll varsa, ağıllı həlli seçmə.**

---

## Versiya 1 — bitmə şərtləri

- [ ] Excel-dən mal siyahısı import olunur
- [ ] Axtarış işləyir (marka + model + il + hissə adı + OEM kod)
- [ ] Sinonim lüğəti işləyir (kolodka = əyləc altlığı = колодки)
- [ ] Hazır cavab qurulur və bir kliklə kopyalanır
- [ ] Mal əlavə / dəyiş / sil (CRUD)
- [ ] Cavab şablonları redaktə olunur
- [ ] Axtarış statistikası: ən çox axtarılan + **tapılmayan** mallar
- [ ] Avtomatik backup (hər açılışda, son 7 gün)
- [ ] Installer hazırlanır (Inno Setup)

---

## Açıq qeydlər (installer addımında unutma!)

- **Baza faylının yeri dəyişməlidir.** Hazırda `zapcavab.db` proqramın öz qovluğunda
  (`bin/Debug/net8.0/`) yaranır — bu, YALNIZ inkişaf mərhələsi üçündür. Real quraşdırmada
  (Program Files-a quraşdırılanda) proqram öz qovluğuna yaza bilməyəcək, çünki adi istifadəçinin
  Program Files-a yazmaq hüququ yoxdur (admin tələb olunur). Installer (Inno Setup) addımında
  baza faylının yolu `%LocalAppData%\ZapCavab\zapcavab.db` (yəni `AppData/Local/ZapCavab/`)
  olaraq dəyişdirilməlidir — bunu `Data/AppDbContext.cs`-dəki `BazaFayliYolu` xassəsində et.
  Backup qovluğu da (`Services/BackupService.cs`) eyni məntiqlə oradan asılıdır, ayrıca
  dəyişməyə ehtiyac yoxdur (baza yolundan avtomatik təyin olunur).

- **RİSK — baza sxem dəyişikliyi.** `Data/AppDbContext.cs`-də `Database.EnsureCreated()`
  istifadə olunur — bu, yalnız baza HEÇ YOXDURSA yaradır, mövcud bazanı YENİLƏYƏ BİLMİR.
  Müştəriyə çatdıqdan sonra `Part` (və ya `Template`) modelinə yeni sahə əlavə etsək,
  proqram çökəcək ("no such column") və ya bazanı silmək lazım gələcək — bu da müştərinin
  bütün mallarının itməsi deməkdir. İnkişaf mərhələsində bu dəfələrlə baş verib (məs.
  `RafYeri` sahəsi əlavə olunanda yerli test bazası əl ilə silinməli oldu).
  **Installer mərhələsindən ƏVVƏL həll olunmalıdır** — ya EF Core Migrations-a keçid,
  ya da sadə versiya yoxlaması (bazada "sxem versiyası" saxlayıb, köhnədirsə əl ilə ALTER
  TABLE ilə yeniləmək). Bunsuz növbəti hər model dəyişikliyi müştəri bazasını poza bilər.

---

## Versiya 1-də OLMAYACAQ

Bunları təklif etmə, əlavə etmə:

- WhatsApp / Instagram API inteqrasiyası (ban riski + baha)
- AI / şəkil tanıma
- Veb və ya mobil versiya
- Çoxlu istifadəçi, rol sistemi
- Barkod, VIN dekoder
- Bulud sinxronizasiyası

---

## Fayl strukturu

```
ZapCavab/
  Models/        Part.cs, Synonym.cs, Template.cs
  Helpers/       TextHelper.cs
  Data/          SynonymDictionary.cs, AppDbContext.cs
  Services/      SearchService.cs, ReplyBuilder.cs, SearchLogger.cs
  Views/         MainWindow.xaml, PartsWindow.xaml, SettingsWindow.xaml
```

---

## Axtarış məntiqi (əsas alqoritm)

1. Mətni normalizasiya et — kiçik hərf, `ə→e ş→s ç→c ğ→g ö→o ü→u ı→i`, artıq işarələri sil
2. Sinonimləri əsas sözə çevir (`əyləc altlığı` → `kolodka`)
3. İli ayır (regex: 1950-2049)
4. Sözlərə böl, hər malla müqayisə et
5. Bal ver: `uyğun söz sayı / ümumi söz sayı`
6. İl uyğun gəlmirsə baldan 0.35 çıx (amma tam atma)
7. Bal < 0.5 olanları göstərmə
8. Sırala: bal → anbarda var → ucuz

---

## Test halları (hər dəyişiklikdən sonra yoxla)

| Giriş | Gözlənilən |
|---|---|
| `prado 2015 kolodka` | 3 ön altlıq |
| `PRADO 2015 KOLODKA` | eyni nəticə |
| `prado ön əyləc altlığı` | eyni nəticə |
| `прадо колодки` | eyni nəticə |
| `prado 2005 kolodka` | tapılır + il xəbərdarlığı |
| `prado 2015 turbo` | nəticə yoxdur |

---

## İşləmə tərzi

Sahib öyrənir. Ona görə:

- Böyük dəyişiklikdən əvvəl **planı izah et, təsdiq gözlə**
- Kod yazandan sonra **1-2 cümlə ilə izah et**
- O səhv yol seçirsə, **de** — amma qərarı ona burax
