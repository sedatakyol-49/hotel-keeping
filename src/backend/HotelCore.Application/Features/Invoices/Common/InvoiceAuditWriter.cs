using System.Text.Json;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Denetim izi yazıcısı (GoBD §6.3) — <b>append-only</b>.
/// <para>
/// <b>Atomiklik:</b> bu sınıf <c>SaveChanges</c> ÇAĞIRMAZ; kaydı yalnızca change tracker'a
/// ekler. Böylece denetim kaydı, tetikleyen işlemle (oluşturma/finalize/ödeme/iptal)
/// <b>aynı SaveChanges</b> — yani aynı transaction — içinde kalıcı olur. İşlem başarısız olursa
/// denetim kaydı da yazılmaz; işlem başarılı olursa denetim kaydı <b>kesin</b> vardır.
/// "İz olmadan işlem" veya "işlem olmadan iz" durumu oluşamaz.
/// </para>
/// <para>
/// <b>Kim/ne zaman:</b> kullanıcı <see cref="ICurrentUser.UserId"/>, zaman
/// <see cref="IDateTimeProvider.UtcNow"/> (UTC) — ikisi de istekten değil sunucudan alınır.
/// </para>
/// <para>
/// <b>Bilinen sınır:</b> <see cref="InvoiceAuditAction"/> enum'ı yalnızca
/// <c>Created/Finalized/Paid/Cancelled</c> içerir. Taslak <b>güncellemesi</b> için karşılık gelen
/// bir aksiyon yoktur; taslak henüz belge değildir (GoBD belge işlevi finalize ile başlar) ve
/// son değişiklik <c>Invoice.ModifiedAt/ModifiedByUserId</c>'de tutulur. Kısmi ödemeler
/// <c>Paid</c> aksiyonuyla, <c>details.fullySettled=false</c> bilgisiyle yazılır — iz kaybetmemek
/// için. Enum'a <c>Updated</c>/<c>PaymentRecorded</c> eklenmesi Domain işidir.
/// </para>
/// </summary>
internal sealed class InvoiceAuditWriter(
    IAppDbContext database,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
{
    /// <summary>Denetim ayrıntısı JSON'u: camelCase, girintisiz (kolon sınırı 4000 karakter).</summary>
    private static readonly JsonSerializerOptions DetailsOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>Kolon sınırı; aşan ayrıntı kesilir (kayıt kaybetmemek için istisna atılmaz).</summary>
    private const int MaxDetailsLength = 4000;

    /// <summary>
    /// Denetim kaydını change tracker'a ekler. Çağıran <c>SaveChangesAsync</c>'i işlemle
    /// birlikte yapar.
    /// </summary>
    public InvoiceAuditEntry Append(Invoice invoice, InvoiceAuditAction action, object? details = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var entry = new InvoiceAuditEntry
        {
            // HotelId aktif otelden DEGIL faturadan alinir: Head Office konsolide modda baska
            // otelin faturasini isleyebilir ve iz o otele yazilmalidir.
            HotelId = invoice.HotelId,
            InvoiceId = invoice.Id,
            Action = action,
            PerformedByUserId = currentUser.UserId,
            PerformedAt = clock.UtcNow,
            Details = Serialize(details)
        };

        database.InvoiceAuditEntries.Add(entry);

        return entry;
    }

    /// <summary>Ayrıntı JSON'unu üretir (aynı kayıt yeniden kullanılırsa güncellenebilir).</summary>
    public static string? Serialize(object? details)
    {
        if (details is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(details, DetailsOptions);

        return json.Length <= MaxDetailsLength ? json : json[..MaxDetailsLength];
    }
}
