using Microsoft.EntityFrameworkCore;
using ZapCavab.Models;

namespace ZapCavab.Data;

// EF Core-un bazaya qoşulmaq üçün istifadə etdiyi mərkəzi sinif.
// Burda "hansı cədvəllər var" (DbSet-lər) və "baza faylı haradadır" təyin olunur.
public class AppDbContext : DbContext
{
    // Cədvəllər — hər DbSet bazada bir cədvələ uyğun gəlir
    public DbSet<Part> Parts { get; set; } = null!;
    public DbSet<Template> Templates { get; set; } = null!;

    // Bu obyektin hansı fayla qoşulacağı. Adətən əsl bazadır, amma testlərdə
    // ayrıca (müvəqqəti) fayl vermək üçün dəyişdirilə bilər.
    private readonly string _bazaYolu;

    // Adi istifadə: heç nə vermədən yaradanda, əsl bazaya qoşulur.
    public AppDbContext() : this(BazaFayliYolu)
    {
    }

    // Test üçün: başqa bir baza faylına qoşulmaq istəyəndə bu istifadə olunur.
    public AppDbContext(string bazaYolu)
    {
        _bazaYolu = bazaYolu;
    }

    // Əsl baza faylının tam yolu. Backup üçün Program.cs-də də istifadə olunur ki,
    // yol iki yerdə ayrı-ayrı yazılmasın.
    // DİQQƏT: hazırda proqramın öz qovluğundadır (bin/Debug/...). Bu, yalnız inkişaf
    // mərhələsi üçün münasibdir — installer addımında AppData/Local/ZapCavab-a
    // köçürülməlidir (CLAUDE.md-dəki "Açıq qeydlər" bölməsinə bax).
    public static string BazaFayliYolu => Path.Combine(AppContext.BaseDirectory, "zapcavab.db");

    // Baza faylının harada saxlanacağını göstərir.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_bazaYolu}");
    }

    // Proqram ilk dəfə açılanda (baza boşdursa) test mallarını əlavə edir.
    // Real istifadədə bu mallar Excel importu ilə əvəz olunacaq.
    public void IlkVeriləriYarat()
    {
        if (Parts.Any())
            return; // artıq mal varsa, təzədən doldurma

        Parts.AddRange(
            new Part { Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
                PartNameAz = "Ön əyləc altlığı", OemCode = "04465-60310", Price = 45, StockQty = 5 },
            new Part { Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
                PartNameAz = "Ön əyləc altlığı (analoq)", OemCode = "04465-60290", Price = 32, StockQty = 0 },
            new Part { Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
                PartNameAz = "Ön əyləc altlığı (orijinal)", OemCode = "D1060-JK50A", Price = 68, StockQty = 2 },
            new Part { Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
                PartNameAz = "Arxa amortizator", OemCode = "48531-69745", Price = 120, StockQty = 3 },
            new Part { Brand = "Toyota", Model = "Camry", YearFrom = 2015, YearTo = 2019,
                PartNameAz = "Yağ filtri", OemCode = "90915-YZZD4", Price = 12, StockQty = 10 },
            new Part { Brand = "Hyundai", Model = "Elantra", YearFrom = 2016, YearTo = 2020,
                PartNameAz = "Ön fara", OemCode = "92101-F2000", Price = 95, StockQty = 1 },
            new Part { Brand = "Toyota", Model = "Prado", YearFrom = 2010, YearTo = 2017,
                PartNameAz = "Ön fara", OemCode = "81150-60J51", Price = 210, StockQty = 1 }
        );

        SaveChanges();
    }
}
