using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.Cancel;

/// <summary>
/// <c>POST /api/v1/invoices/{id}/cancel</c> — iptal.
/// <list type="bullet">
///   <item><b>Taslak:</b> doğrudan <c>Cancelled</c> olur. Numara almadığı için ortada bir belge
///   yoktur; iptal faturası (Stornorechnung) gerekmez ve sekansta boşluk oluşmaz.</item>
///   <item><b>Kesinleşmiş/Ödenmiş:</b> orijinal <b>korunur</b>; tutarları negatif olan yeni bir
///   <b>Stornorechnung</b> oluşturulup kesinleştirilir ve orijinal ona
///   <c>CancelledByInvoiceId</c> ile bağlanır (GoBD §6.1).</item>
/// </list>
/// </summary>
public sealed record CancelInvoiceRequest : IRequest<InvoiceDetailResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>
    /// İptal gerekçesi (opsiyonel, ≤ 500). Denetim izine yazılır — GoBD açısından "neden"
    /// bilgisi belge tarihçesinin parçasıdır, bu yüzden serbest metin olsa da saklanır.
    /// </summary>
    public string? Reason { get; init; }
}
