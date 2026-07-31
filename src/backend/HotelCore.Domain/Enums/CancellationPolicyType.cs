namespace HotelCore.Domain.Enums;

/// <summary>
/// İptal politikasının <b>türü</b> — misafire gösterilen etiketin ve politika metni anahtarının
/// kaynağı (api-contracts-public-booking.md §4.3 <c>PublicCancellationPolicy.type</c>).
/// <para>
/// <b>Neden bir bool değil:</b> bu fazda politika otel bazında tektir, ama sözleşme
/// <c>type</c> alanını bilinçli olarak genişlemeye açık bırakır
/// (architecture-public-booking.md §12: plan bazlı "non-refundable" tarife sonraki faz).
/// Bool bir alan o genişlemeyi taşıyamazdı.
/// </para>
/// </summary>
public enum CancellationPolicyType
{
    /// <summary>Belirlenen son tarihe kadar ücretsiz iptal edilebilir.</summary>
    Flexible = 0,

    /// <summary>Ücretsiz iptal penceresi yok; her iptal ücrete tabidir.</summary>
    Restricted = 1
}
