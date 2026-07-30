namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Denetim izi kaydı (GoBD §6.3). Salt-okunurdur: bu kayıtlar hiçbir uç noktadan
/// güncellenemez/silinemez.
/// </summary>
public sealed record InvoiceAuditEntryResponse
{
    public Guid Id { get; init; }

    /// <summary>İşlem enum <b>adı</b>: <c>Created | Finalized | Paid | Cancelled</c>.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>İşlemi yapan kullanıcı (kimlik yoksa null — örn. sistem/seed işlemleri).</summary>
    public Guid? PerformedByUserId { get; init; }

    public DateTimeOffset PerformedAt { get; init; }

    /// <summary>Serbest biçimli JSON ayrıntı (tutarlar, önceki/sonraki durum, gerekçe).</summary>
    public string? Details { get; init; }
}
