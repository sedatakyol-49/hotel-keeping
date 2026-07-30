using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Update;

/// <summary>
/// Taslak faturayı günceller (satırlar tamamen değiştirilir, tutarlar yeniden hesaplanır).
/// <para>
/// <b>GoBD §6.1:</b> yalnızca <c>Draft</c> düzenlenebilir; <c>Finalized/Paid/Cancelled</c> için
/// <b>409</b> döner. Bu kural burada anlamlı mesajla, ayrıca <c>AppDbContext</c> guard'ında
/// son savunma olarak zorlanır.
/// </para>
/// <para>
/// <b>Denetim izi:</b> değişiklik <see cref="InvoiceAuditAction.Updated"/> olarak yazılır
/// (değişen alanlar + eski/yeni tutarlar + satır sayısı). Taslak henüz <i>belge</i> olmadığı için
/// bu kayıt GoBD açısından zorunlu değildir; <i>Nachvollziehbarkeit</i> (izlenebilirlik) için
/// tutulur: bir faturanın hangi tutarla oluşup hangi tutarla kesinleştiği yalnızca
/// <c>ModifiedAt/ModifiedByUserId</c> ile açıklanamaz. Kayıt, güncellemeyle <b>aynı
/// SaveChanges</b> içinde yazılır (bkz. <see cref="InvoiceAuditWriter"/>).
/// </para>
/// </summary>
internal sealed class UpdateInvoiceHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    InvoiceReader reader,
    InvoiceLineComposer composer,
    InvoiceAuditWriter audit)
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

        // Denetim izi icin ONCEKI hal: tutarlar ve satir sayisi degistirilmeden once okunur.
        var before = new InvoiceSnapshot(
            invoice.GuestId,
            invoice.Culture,
            invoice.LineItems.Count,
            invoice.NetAmount,
            invoice.VatAmount,
            invoice.CityTaxAmount,
            invoice.GrossAmount);

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

        audit.Append(invoice, InvoiceAuditAction.Updated, new
        {
            changedFields = CollectChangedFields(before, invoice, replacementLines.Count),
            guestId = new
            {
                old = before.GuestId,
                @new = invoice.GuestId
            },
            culture = new
            {
                old = before.Culture,
                @new = invoice.Culture
            },
            lineCount = new
            {
                old = before.LineCount,
                @new = replacementLines.Count
            },
            netAmount = new { old = before.NetAmount, @new = invoice.NetAmount },
            vatAmount = new { old = before.VatAmount, @new = invoice.VatAmount },
            cityTaxAmount = new { old = before.CityTaxAmount, @new = invoice.CityTaxAmount },
            grossAmount = new { old = before.GrossAmount, @new = invoice.GrossAmount },
            currency = invoice.Currency
        });

        // Fatura + satirlar + denetim izi TEK transaction.
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetDetailAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gerçekten değişen alanların adları. Satırlar PUT semantiği gereği <b>her zaman</b> yeniden
    /// yazıldığı için "lineItems" değişiklik sayılır; tutar alanları yalnızca değer farklıysa
    /// listelenir (aynı satırlar yeniden gönderildiğinde iz gürültü üretmesin).
    /// </summary>
    private static List<string> CollectChangedFields(
        InvoiceSnapshot before,
        Invoice after,
        int newLineCount)
    {
        var changed = new List<string>(6) { "lineItems" };

        if (before.GuestId != after.GuestId)
        {
            changed.Add("guestId");
        }

        if (!string.Equals(before.Culture, after.Culture, StringComparison.Ordinal))
        {
            changed.Add("culture");
        }

        if (before.LineCount != newLineCount)
        {
            changed.Add("lineCount");
        }

        if (before.NetAmount != after.NetAmount)
        {
            changed.Add("netAmount");
        }

        if (before.VatAmount != after.VatAmount)
        {
            changed.Add("vatAmount");
        }

        if (before.CityTaxAmount != after.CityTaxAmount)
        {
            changed.Add("cityTaxAmount");
        }

        if (before.GrossAmount != after.GrossAmount)
        {
            changed.Add("grossAmount");
        }

        return changed;
    }

    /// <summary>Güncelleme öncesi taslak hâli (denetim izinde "eski" değerler).</summary>
    private sealed record InvoiceSnapshot(
        Guid GuestId,
        string Culture,
        int LineCount,
        decimal NetAmount,
        decimal VatAmount,
        decimal CityTaxAmount,
        decimal GrossAmount);
}
