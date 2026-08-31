namespace ZapCavab.Models;

// Dükanda satılan bir mal (ehtiyat hissəsi) haqqında məlumat.
// Excel-dən import zamanı da, əl ilə əlavə edərkən də bu struktur istifadə olunacaq.
public class Part
{
    public int Id { get; set; }

    // Marka: məsələn "Toyota"
    public string Brand { get; set; } = string.Empty;

    // Model: məsələn "Prado"
    public string Model { get; set; } = string.Empty;

    // Malın hansı il aralığına uyğun olduğu (məsələn 2010-2017)
    public int YearFrom { get; set; }
    public int YearTo { get; set; }

    // Malın adı, müştəriyə göstəriləcək formada (məsələn "Ön əyləc altlığı")
    public string PartNameAz { get; set; } = string.Empty;

    // Zavod (OEM) kodu — dəqiq axtarış üçün
    public string OemCode { get; set; } = string.Empty;

    // Qiymət (AZN)
    public decimal Price { get; set; }

    // Anbarda neçə ədəd qaldığı
    public int StockQty { get; set; }

    // Anbarda olub-olmadığını tez yoxlamaq üçün köməkçi xassə
    public bool InStock => StockQty > 0;
}
