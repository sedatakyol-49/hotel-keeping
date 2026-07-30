using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Cancel;

/// <summary>
/// Faturayı iptal eder — GoBD §6.1.
///
/// <para><b>Taslak vs. kesinleşmiş ayrımı:</b> taslak fatura henüz <i>belge</i> değildir
/// (numarası yoktur, muhasebeye girmemiştir), bu yüzden doğrudan <c>Cancelled</c> yapılır ve
/// sekansta boşluk oluşmaz. Kesinleşmiş fatura ise <b>değiştirilemez ve silinemez</b>; tek meşru
/// düzeltme yolu tutarları negatif olan yeni bir belge — <b>Stornorechnung</b> — kesmektir.
/// Orijinal aynen korunur ve <c>CancelledByInvoiceId</c> ile iptal faturasına bağlanır.</para>
///
/// <para><b>Storno tasarımı:</b> iptal faturasının satırları orijinalin <b>ayna görüntüsüdür</b>:
/// aynı açıklama ("Storno: ..." önekiyle), aynı miktar, işareti çevrilmiş birim fiyat ve
/// <b>doğrudan negatiflenmiş</b> <c>LineNet</c>/<c>LineVat</c> değerleri. Tutarlar yeniden
/// hesaplanmaz, kopyalanıp negatiflenir; böylece orijinal + storno toplamı <b>tam olarak sıfır</b>
/// olur (yeniden hesaplamada oran değişmişse kuruş farkı doğabilirdi).</para>
///
/// <para><b>Neden iki SaveChanges:</b> <c>AppDbContext.EnforceInvoiceImmutability</c> guard'ı,
/// durumu <c>Finalized</c> olan bir faturaya satır eklenmesini reddeder — <i>yeni</i> eklenen bir
/// fatura için de öyle. Bu yüzden storno önce <b>taslak</b> olarak satırlarıyla kaydedilir
/// (1. transaction), ardından normal finalize yolundan geçirilir (2. transaction) ve orijinalin
/// iptali <b>aynı</b> ikinci transaction'da yazılır. Numara yalnızca 2. adımda tüketildiği için
/// 1. adım kalıp 2. adım başarısız olsa bile <b>sekansta boşluk oluşmaz</b>; ortada yalnızca
/// numarasız (belge olmayan) bir taslak kalır ve orijinal iptal edilmemiş sayılır — yani sistem
/// tutarlı bir durumda kalır.</para>
/// </summary>
internal sealed class CancelInvoiceHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    InvoiceReader reader,
    IInvoiceNumberGenerator numberGenerator,
    InvoiceAuditWriter audit)
    : IRequestHandler<CancelInvoiceRequest, InvoiceDetailResponse>
{
    /// <summary>Storno satır açıklamalarının öneki (dil-nötr, kısaltılmadan saklanır).</summary>
    private const string StornoPrefix = "Storno: ";

    /// <summary>Açıklama kolonu sınırı (500) — önek eklendiğinde taşmaması için.</summary>
    private const int MaxDescriptionLength = 500;

    public async Task<InvoiceDetailResponse> Handle(
        CancelInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoice = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (invoice.Status is InvoiceStatus.Cancelled)
        {
            throw new ConflictException("Fatura zaten iptal edilmis.");
        }

        if (invoice.Status is InvoiceStatus.Draft)
        {
            await CancelDraftAsync(invoice, request.Reason, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await CancelFinalizedAsync(invoice, request.Reason, cancellationToken).ConfigureAwait(false);
        }

        // Orijinal fatura dondurulur: iptal baglantisi (cancelledByInvoiceId) burada gorunur.
        return await reader.GetDetailAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Taslak iptali: numara tüketilmediği için storno gerekmez. Folio kaynaklı satırlar folio'ya
    /// geri bırakılır ki masraf kaybolmasın ve yeniden faturalanabilsin.
    /// </summary>
    private async Task CancelDraftAsync(Invoice invoice, string? reason, CancellationToken cancellationToken)
    {
        // Faturaya ozgu satirlar KORUNUR (taslagin neyi icerdigi kaydin parcasidir);
        // yalnizca folio masraflari geri birakilir.
        InvoiceLineComposer.ReleaseFolioLines(invoice);
        InvoiceAmounts.ApplyTotals(invoice, invoice.LineItems);

        invoice.MarkCancelled();

        audit.Append(invoice, InvoiceAuditAction.Cancelled, new
        {
            previousStatus = nameof(InvoiceStatus.Draft),
            stornoRequired = false,
            note = "Taslak fatura numara almadigi icin iptal faturasi (Stornorechnung) gerekmez.",
            reason
        });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Kesinleşmiş/ödenmiş fatura iptali: Stornorechnung üretilir.</summary>
    private async Task CancelFinalizedAsync(
        Invoice invoice,
        string? reason,
        CancellationToken cancellationToken)
    {
        var previousStatus = invoice.Status;

        // --- 1. transaction: iptal faturasi TASLAK olarak satirlariyla yazilir ----------------
        var storno = new Invoice
        {
            HotelId = invoice.HotelId,
            InvoiceNumber = string.Empty,
            ReservationId = invoice.ReservationId,
            GuestId = invoice.GuestId,
            Culture = invoice.Culture,
            Currency = invoice.Currency,
            NetAmount = -invoice.NetAmount,
            VatAmount = -invoice.VatAmount,
            CityTaxAmount = -invoice.CityTaxAmount,
            GrossAmount = -invoice.GrossAmount,
        };

        foreach (var line in invoice.LineItems.OrderBy(line => line.SortOrder).ThenBy(line => line.Id))
        {
            storno.LineItems.Add(new InvoiceLineItem
            {
                HotelId = line.HotelId,
                Type = line.Type,
                Description = Truncate(StornoPrefix + line.Description),
                Quantity = line.Quantity,
                UnitPrice = -line.UnitPrice,
                VatRate = line.VatRate,
                // Ayna: yeniden hesaplanmaz, negatiflenir -> orijinal + storno = 0 (kurusu kurusuna).
                LineNet = -line.LineNet,
                LineVat = -line.LineVat,
                ServiceDate = line.ServiceDate,
                SortOrder = line.SortOrder,
                // FolioId TASINMAZ: storno bir muhasebe belgesidir, folio masrafi degildir.
            });
        }

        database.Invoices.Add(storno);

        audit.Append(storno, InvoiceAuditAction.Created, new
        {
            kind = "Stornorechnung",
            cancelsInvoiceId = invoice.Id,
            cancelsInvoiceNumber = invoice.InvoiceNumber,
            grossAmount = storno.GrossAmount,
            currency = storno.Currency,
            reason
        });

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // --- 2. transaction: storno kesinlesir + orijinal iptal edilir (ATOMIK) ---------------
        var issuedAt = clock.UtcNow;

        var stornoNumber = await numberGenerator
            .NextNumberAsync(storno.HotelId, issuedAt.UtcDateTime.Year, cancellationToken)
            .ConfigureAwait(false);

        storno.MarkFinalized(stornoNumber, issuedAt);

        // Domain guard: kesinlesmis fatura ancak iptal faturasi kimligi verilerek iptal edilebilir.
        invoice.MarkCancelled(storno.Id);

        audit.Append(storno, InvoiceAuditAction.Finalized, new
        {
            invoiceNumber = stornoNumber,
            issuedAt,
            kind = "Stornorechnung",
            cancelsInvoiceId = invoice.Id,
            grossAmount = storno.GrossAmount
        });

        audit.Append(invoice, InvoiceAuditAction.Cancelled, new
        {
            previousStatus = previousStatus.ToString(),
            stornoRequired = true,
            cancelledByInvoiceId = storno.Id,
            cancelledByInvoiceNumber = stornoNumber,
            grossAmount = invoice.GrossAmount,
            reason
        });

        await InvoicePersistence
            .SaveWithNumberSequenceGuardAsync(database, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxDescriptionLength ? value : value[..MaxDescriptionLength];
}
