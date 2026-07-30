using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Guests.Common;

/// <summary>
/// Misafir sorgusunun düz izdüşümü (yalnızca gereken kolonlar). Enum → string dönüşümü
/// bilinçli olarak <b>SQL'de değil</b> C# tarafında yapılır: <c>Country?</c> gibi nullable
/// enum'ların <c>ToString()</c> çevirisi sağlayıcıya göre değişebilir, satırı ham okumak
/// davranışı deterministik kılar.
/// </summary>
/// <param name="Id">Misafir kimliği.</param>
/// <param name="FirstName">Ad.</param>
/// <param name="LastName">Soyad.</param>
/// <param name="Email">E-posta.</param>
/// <param name="Phone">Telefon.</param>
/// <param name="Nationality">Uyruk (enum).</param>
/// <param name="AddressLine">Adres satırı.</param>
/// <param name="PostalCode">Posta kodu.</param>
/// <param name="City">Şehir.</param>
/// <param name="BirthDate">Doğum tarihi.</param>
/// <param name="Culture">Yazışma dili.</param>
/// <param name="Note">Serbest not.</param>
internal sealed record GuestRow(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    Country? Nationality,
    string? AddressLine,
    string? PostalCode,
    string? City,
    DateOnly? BirthDate,
    string? Culture,
    string? Note);
