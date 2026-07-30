namespace HotelCore.Application.Features.Guests.Common;

/// <summary>
/// Misafir — api-contracts-reservations.md → "Guests" ile birebir.
/// </summary>
public sealed record GuestResponse
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    /// <summary>Görüntüleme için hazır ad — istemcinin birleştirmesi gerekmez.</summary>
    public string FullName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>Uyruk: <c>Country</c> enum <b>adı</b> (string), sayı değil.</summary>
    public string? Nationality { get; init; }

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? City { get; init; }

    public DateOnly? BirthDate { get; init; }

    /// <summary>Yazışma/fatura dili (<c>de|en|tr</c>).</summary>
    public string? Culture { get; init; }

    public string? Note { get; init; }

    /// <summary>
    /// Geçmiş konaklama sayısı — <b>sunucuda hesaplanır</b> (entity'de kolon olarak tutulmaz,
    /// architecture.md §4.3). Yalnızca <c>GET /guests/{id}</c> yanıtında doldurulur; liste
    /// yanıtında <c>null</c>'dır (her satır için korele alt sorgu çalıştırmamak için).
    /// <para>
    /// Tanım: <c>CheckedOut</c> durumundaki rezervasyon sayısı — yani gerçekten tamamlanmış
    /// konaklamalar. İptal/gelmedi ve henüz gelecekteki rezervasyonlar sayılmaz.
    /// </para>
    /// </summary>
    public int? StayCount { get; init; }
}
