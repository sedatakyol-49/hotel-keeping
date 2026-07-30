using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Hotels.UpdateSettings;

/// <summary>
/// <c>PUT /api/v1/hotels/{id}/settings</c> gövdesi.
/// <para>
/// Vergi oranları burada yönetilir; koda hardcode edilmez (architecture.md §4.1).
/// </para>
/// </summary>
public sealed record UpdateHotelSettingsRequest : IRequest<HotelResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public Country Country { get; init; }

    public string City { get; init; } = string.Empty;

    public string? AddressLine { get; init; }

    public string? PostalCode { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? TaxNumber { get; init; }

    public string DefaultCulture { get; init; } = string.Empty;

    /// <summary>ISO 4217 kodu; büyük harfe normalize edilerek saklanır.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>
    /// Vergi profili — yanıttaki <see cref="TaxProfileDto"/> ile <b>aynı şekildir</b> (oku-yaz
    /// simetrisi: GET'ten alınan gövde doğrudan geri gönderilebilir).
    /// <para>
    /// <c>cityTaxExemptChildren</c> ve <c>cityTaxChildAgeLimit</c> gönderilmezse varsayılanlarına
    /// (<c>false</c> / <c>null</c>) düşer — PUT tam değişim semantiğindedir, kısmi güncelleme yoktur.
    /// </para>
    /// </summary>
    public TaxProfileDto TaxProfile { get; init; } = new();
}
