namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Denetim izi kaydı (GoBD §6.3). Salt-okunurdur: bu kayıtlar hiçbir uç noktadan
/// güncellenemez/silinemez.
/// </summary>
public sealed record InvoiceAuditEntryResponse
{
    public Guid Id { get; init; }

    /// <summary>
    /// İşlem enum <b>adı</b>:
    /// <c>Created | Updated | Finalized | PaymentRecorded | Paid | Cancelled</c>.
    /// <para>
    /// <c>PaymentRecorded</c> her tahsilat olayıdır (kısmi ödeme dâhil); <c>Paid</c> yalnızca
    /// bakiye kapandığındaki <b>durum geçişi</b>dir — bakiyeyi kapatan ödeme için iki kayıt oluşur.
    /// </para>
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>İşlemi yapan kullanıcı (kimlik yoksa null — örn. sistem/seed işlemleri).</summary>
    public Guid? PerformedByUserId { get; init; }

    public DateTimeOffset PerformedAt { get; init; }

    /// <summary>Serbest biçimli JSON ayrıntı (tutarlar, önceki/sonraki durum, gerekçe).</summary>
    public string? Details { get; init; }
}
