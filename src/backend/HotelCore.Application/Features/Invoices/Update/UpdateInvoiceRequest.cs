using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.Update;

/// <summary>
/// <c>PUT /api/v1/invoices/{id}</c> — <b>yalnızca taslak</b> fatura düzenlenebilir.
/// Kesinleşmiş faturaya PUT → <b>409</b> (GoBD §6.1).
/// <para>
/// <b>Elle</b> kesilen faturada satırlar <b>tamamen değiştirilir</b> (PUT semantiği).
/// <b>Rezervasyondan üretilen</b> faturada gövde yalnızca <c>Extra</c> satırları değiştirir;
/// <c>RoomCharge</c> ve <c>CityTax</c> sunucunundur ve korunur (gövdede gelirlerse <b>400</b>) —
/// gerekçe: <see cref="UpdateInvoiceHandler"/>.
/// </para>
/// <para>
/// Rezervasyon bağı (<c>reservationId</c>) değiştirilemez — faturanın kaynağı sonradan başka bir
/// rezervasyona kaydırılamaz.
/// </para>
/// </summary>
public sealed record UpdateInvoiceRequest : IRequest<InvoiceDetailResponse>, IInvoiceWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>
    /// Misafir. Verilmezse mevcut misafir korunur. Rezervasyona bağlı faturada
    /// değiştirilemez (409).
    /// </summary>
    public Guid? GuestId { get; init; }

    /// <summary>Fatura dili (<c>de|en|tr</c>); verilmezse mevcut dil korunur.</summary>
    public string? Culture { get; init; }

    /// <summary>
    /// Yeni satır kümesi. Fatura <b>sonuçta</b> en az bir satır içermelidir; rezervasyondan
    /// üretilen faturada sunucunun satırları korunduğu için boş dizi geçerlidir
    /// ("elle eklenen tüm ekstraları kaldır").
    /// </summary>
    public IReadOnlyList<InvoiceLineInput> LineItems { get; init; } = [];
}
