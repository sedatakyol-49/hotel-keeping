using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura yazma işlemlerinin ortak kaydetme yolu ve durum ön kontrolleri.
/// </summary>
internal static class InvoicePersistence
{
    /// <summary>
    /// Numara tüketen işlemler (finalize / storno finalize) için kaydetme.
    /// <para>
    /// <c>DbUpdateConcurrencyException</c> → <b>409</b>: eşzamanlı bir başka finalize aynı otel/yıl
    /// sayacını güncellemiş. Transaction tamamen geri alındığı için <b>numara tüketilmemiştir</b>
    /// (boşluk oluşmaz); istemci isteği tekrarlayarak sıradaki numarayı alır. Otomatik retry
    /// yapılmaz — gerekçe: <see cref="HotelInvoiceCounterContract"/>.
    /// </para>
    /// </summary>
    public static async Task SaveWithNumberSequenceGuardAsync(
        IAppDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(Messages.InvoiceNumberSequenceRace, exception);
        }
    }

    /// <summary>
    /// Yalnızca taslak faturanın değiştirilebileceğini doğrular (GoBD §6.1). Kesinleşmiş faturaya
    /// yapılan güncelleme denemesi <b>409</b> döner — <c>AppDbContext</c> guard'ı da aynı kuralı
    /// ikinci kez zorlar, ama kullanıcıya anlamlı mesaj burada üretilir.
    /// </summary>
    public static void EnsureDraft(InvoiceStatus status)
    {
        if (status is InvoiceStatus.Draft)
        {
            return;
        }

        throw new ConflictException(Messages.InvoiceNotDraft(status));
    }
}
