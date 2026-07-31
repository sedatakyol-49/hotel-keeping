using System.Diagnostics.CodeAnalysis;

// CA1716 — test ad alani, olculen uretim ad alanini AYNEN yansitir
// (HotelCore.Application.Features.Public). Testi baska bir adla toplamak, "hangi modulun testi"
// sorusunu kod tabaninda ikinci bir isimlendirmeye baglardi.
[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Test ad alani olculen uretim ad alanini yansitir; yalnizca C# tuketilir.",
    Scope = "namespaceanddescendants",
    Target = "~N:HotelCore.Application.Tests.Public")]
