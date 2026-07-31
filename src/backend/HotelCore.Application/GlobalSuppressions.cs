using System.Diagnostics.CodeAnalysis;

// CA1716 — "Public" bir dil anahtar kelimesiyle çakışıyor.
//
// Namespace ağacı bilinçli olarak `HotelCore.Application.Features.Public.*`'tır: misafire açık
// kanalın tüm tipleri tek bir ad alanı altında toplanır ve bu ayrım sözleşmenin kendisidir
// (architecture-public-booking.md §4.3 — public DTO'lar admin DTO'larından TAMAMEN ayrıdır).
// Ayrım hem bir mimari test (public tipler admin namespace'inden tip referanslayamaz) hem de
// ikinci OpenAPI belgesinin (`public-v1`) grup filtresi tarafından kullanılır.
//
// Kuralın gerekçesi "başka .NET dillerinden tüketim zorlaşır"dır; bu kod tabanı yalnızca C#'tır
// ve namespace'ler dış bir pakete yayınlanmaz. Adı değiştirmek (örn. `PublicChannel`) sözleşme
// dosyalarındaki klasör yolunu ve dört ajanın ortak referansını kırardı.
[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification =
        "Misafire acik kanalin ad alani sozlesmenin parcasidir (architecture-public-booking.md " +
        "§4.3); yalnizca C# tuketilir ve namespace disa yayinlanmaz.",
    Scope = "namespaceanddescendants",
    Target = "~N:HotelCore.Application.Features.Public")]
