using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.Update;

/// <summary>
/// Taslak faturayı günceller (satırlar tamamen değiştirilir, tutarlar yeniden hesaplanır).
/// <para>
/// <b>GoBD §6.1:</b> yalnızca <c>Draft</c> düzenlenebilir; <c>Finalized/Paid/Cancelled</c> için
/// <b>409</b> döner. Bu kural burada anlamlı mesajla, ayrıca <c>AppDbContext</c> guard'ında
/// son savunma olarak zorlanır.
/// </para>
/// <para>
/// <b>Denetim izi notu:</b> taslak güncellemesi için <c>InvoiceAuditAction</c> içinde karşılık
/// gelen bir aksiyon yoktur (Created/Finalized/Paid/Cancelled). Taslak henüz belge değildir;
/// son değişiklik <c>Invoice.ModifiedAt/ModifiedByUserId</c>'de tutulur. Bkz.
/// <see cref="InvoiceAuditWriter"/> ve raporlanan Domain önerisi.
/// </para>
/// </summary>
internal sealed class UpdateInvoiceHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    InvoiceReader reader,
    InvoiceLineComposer composer)
    : IRequestHandler<UpdateInvoiceRequest, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        UpdateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoice = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        InvoicePersistence.EnsureDraft(invoice.Status);

        var tax = await reader.GetTaxContextAsync(invoice.HotelId, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        if (request.GuestId is Guid guestId && guestId != invoice.GuestId)
        {
            if (invoice.ReservationId is not null)
            {
                throw new ConflictException(
                    "Rezervasyona bagli faturanin misafiri degistirilemez; " +
                    "misafir rezervasyondan gelir.");
            }

            _ = await reader.GetGuestAsync(guestId, cancellationToken).ConfigureAwait(false);
            invoice.GuestId = guestId;
        }

        if (SupportedCultures.IsSupported(request.Culture))
        {
            invoice.Culture = SupportedCultures.Normalize(request.Culture!);
        }

        // Tam degisim: folio kaynakli satirlar folio'ya geri doner (silinmez), faturaya ozgu
        // satirlar silinir ve yerlerine gonderilen satirlar yazilir.
        InvoiceLineComposer.ReleaseFolioLines(invoice);
        composer.RemoveOwnLines(invoice);

        var replacementLines = InvoiceLineComposer.BuildManualLines(
            invoice.HotelId,
            tax,
            request.LineItems,
            today);

        foreach (var line in replacementLines)
        {
            // DIKKAT (iki ayri EF Core tuzagi):
            // (1) Yeni satiri YALNIZCA navigation koleksiyonuna eklemek yetmez: anahtarlar
            //     uygulamada uretildigi icin (EntityBase.Id = Guid.NewGuid()) EF, izlenen ve
            //     durumu Modified/Unchanged olan bir ebeveynin altinda buldugu "anahtari dolu"
            //     cocugu Added degil MODIFIED sayar -> INSERT yerine UPDATE (0 satir -> hata).
            //     Bu yuzden satir DbSet'e eklenir; durumu kesin Added olur.
            // (2) InvoiceId atandiginda EF fixup satiri invoice.LineItems'a KENDISI ekler; ayrica
            //     elle eklemek koleksiyonda cift kayit ve tutarlarin iki katina cikmasina yol
            //     acar. Bu yuzden koleksiyona elle EKLENMEZ ve toplamlar asagida acik listeden
            //     hesaplanir.
            line.InvoiceId = invoice.Id;
            database.InvoiceLineItems.Add(line);
        }

        // Toplamlar navigation koleksiyonundan degil ACIK listeden hesaplanir: mevcut satirlar
        // yukarida tamamen kaldirildigi icin faturanin nihai satir kumesi tam olarak budur.
        InvoiceAmounts.ApplyTotals(invoice, replacementLines);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetDetailAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
    }
}
