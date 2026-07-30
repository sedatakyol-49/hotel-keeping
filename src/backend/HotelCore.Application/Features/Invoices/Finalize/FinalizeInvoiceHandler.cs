using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Finalize;

/// <summary>
/// Taslağı kesinleştirir (GoBD §6.1 + §6.2).
/// <para>
/// <b>Atomiklik:</b> sayaç artışı (numara), faturanın numarası/tarihi/durumu ve
/// <c>InvoiceAuditEntry(Finalized)</c> <b>tek</b> <c>SaveChanges</c> — yani tek transaction —
/// içinde yazılır. Herhangi bir adım başarısız olursa hiçbiri kalıcı olmaz; bu yüzden numara
/// atlanamaz. Eşzamanlı ikinci finalize sayacın <c>Version</c> token'ında yakalanır ve
/// <b>409</b> döner (bkz. <see cref="InvoicePersistence.SaveWithNumberSequenceGuardAsync"/>).
/// </para>
/// <para>
/// <b>Yıl:</b> sekans otel + yıl bazındadır; yıl <c>IssuedAt</c>'in <b>UTC</b> yılıdır (otel saat
/// dilimi henüz modellenmemiştir — yıl dönümünde ±saatlik fark ürün kararı olarak raporlanmıştır).
/// </para>
/// </summary>
internal sealed class FinalizeInvoiceHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    InvoiceReader reader,
    IInvoiceNumberGenerator numberGenerator,
    InvoiceAuditWriter audit)
    : IRequestHandler<FinalizeInvoiceRequest, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        FinalizeInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoice = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (invoice.Status is not InvoiceStatus.Draft)
        {
            throw new ConflictException(Messages.InvoiceNotDraftForFinalize(invoice.Status));
        }

        if (invoice.LineItems.Count == 0)
        {
            throw new ConflictException(Messages.InvoiceWithoutLines);
        }

        // Tutarlar SUNUCUDA yeniden hesaplanir: taslak uzerinde kalmis eski bir toplam
        // kesinlesmis belgeye tasinmasin.
        InvoiceAmounts.ApplyTotals(invoice, invoice.LineItems);

        var issuedAt = clock.UtcNow;

        var invoiceNumber = await numberGenerator
            // Numara faturanin otelinden alinir (aktif otelden degil): Head Office konsolide
            // modda baska otelin faturasini kesinlestirebilir.
            .NextNumberAsync(invoice.HotelId, issuedAt.UtcDateTime.Year, cancellationToken)
            .ConfigureAwait(false);

        invoice.MarkFinalized(invoiceNumber, issuedAt);

        audit.Append(invoice, InvoiceAuditAction.Finalized, new
        {
            invoiceNumber,
            issuedAt,
            netAmount = invoice.NetAmount,
            vatAmount = invoice.VatAmount,
            cityTaxAmount = invoice.CityTaxAmount,
            grossAmount = invoice.GrossAmount,
            currency = invoice.Currency,
            lineCount = invoice.LineItems.Count
        });

        await InvoicePersistence
            .SaveWithNumberSequenceGuardAsync(database, cancellationToken)
            .ConfigureAwait(false);

        return await reader.GetDetailAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
    }
}
