using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Create;

/// <summary>
/// Taslak fatura oluşturur.
/// <para>
/// <b>Numara atanmaz</b> (GoBD §6.2: sekansta boşluk oluşmaması için numara finalize anında
/// verilir). <b>Tutarlar sunucuda</b> satırlardan hesaplanır; istemci toplam göndermez.
/// <b>Vergi oranları</b> otelin <c>TaxProfile</c>'ından okunur (hardcode yok).
/// Oluşturma <c>InvoiceAuditEntry(Created)</c> olarak <b>aynı SaveChanges</b> içinde iz bırakır.
/// </para>
/// </summary>
internal sealed class CreateInvoiceHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    InvoiceReader reader,
    InvoiceLineComposer composer,
    InvoiceAuditWriter audit)
    : IRequestHandler<CreateInvoiceRequest, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Konsolide modda faturanin hangi otele yazilacagi belirsizdir -> 400.
        var hotelId = currentUser.RequireHotelId();
        var tax = await reader.GetTaxContextAsync(hotelId, cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var invoice = new Invoice
        {
            HotelId = hotelId,
            // Taslakta numara BOS: filtreli unique index bu yuzden bos degeri saymaz.
            InvoiceNumber = string.Empty,
            Currency = tax.Currency,
        };

        List<InvoiceLineItem> newLines;
        List<InvoiceLineItem> folioLines;
        string? guestCulture;

        if (request.ReservationId is Guid reservationId)
        {
            var charges = await composer
                .BuildFromReservationAsync(reservationId, tax, cancellationToken)
                .ConfigureAwait(false);

            invoice.ReservationId = charges.Source.Id;
            invoice.GuestId = charges.Source.GuestId;

            var guest = await reader.GetGuestAsync(charges.Source.GuestId, cancellationToken)
                .ConfigureAwait(false);
            guestCulture = guest.Culture;

            newLines = charges.NewLines;
            folioLines = charges.FolioLines;
        }
        else
        {
            // Validator guestId'nin dolu oldugunu garanti eder.
            var guestId = request.GuestId!.Value;
            var guest = await reader.GetGuestAsync(guestId, cancellationToken).ConfigureAwait(false);

            invoice.GuestId = guestId;
            guestCulture = guest.Culture;

            newLines = InvoiceLineComposer.BuildManualLines(hotelId, tax, request.LineItems, today);
            folioLines = [];
        }

        invoice.Culture = ResolveCulture(request.Culture, guestCulture, tax.DefaultCulture);

        foreach (var line in newLines)
        {
            line.HotelId = invoice.HotelId;
            // Fatura henuz izlenmiyor: Add(invoice) tum grafigi Added olarak isaretleyecek.
            invoice.LineItems.Add(line);
        }

        foreach (var folioLine in folioLines)
        {
            // Folio satiri faturaya TASINIR: FolioId korunur (masrafin kaynagi izlenebilir kalir).
            // Bu satirlar zaten izleniyor; durumlari Modified kalir (UPDATE).
            folioLine.InvoiceId = invoice.Id;
            invoice.LineItems.Add(folioLine);
        }

        // Toplamlar ACIK listeden hesaplanir (navigation koleksiyonu EF fixup'i nedeniyle
        // beklenmeyen sekilde degisebilir; bkz. UpdateInvoiceHandler'daki not).
        List<InvoiceLineItem> allLines = [.. newLines, .. folioLines];
        InvoiceAmounts.ApplyTotals(invoice, allLines);

        database.Invoices.Add(invoice);

        audit.Append(invoice, InvoiceAuditAction.Created, new
        {
            source = request.ReservationId is null ? "manual" : "reservation",
            reservationId = invoice.ReservationId,
            guestId = invoice.GuestId,
            lineCount = allLines.Count,
            netAmount = invoice.NetAmount,
            vatAmount = invoice.VatAmount,
            cityTaxAmount = invoice.CityTaxAmount,
            grossAmount = invoice.GrossAmount,
            currency = invoice.Currency,
            vatRates = new
            {
                standard = tax.VatRate,
                reduced = tax.ReducedVatRate,
                cityTaxPerPersonNight = tax.CityTaxEnabled ? tax.CityTaxPerPersonNight : 0m
            }
        });

        // Fatura + satirlar + denetim izi TEK transaction.
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetDetailAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fatura dili: istek → misafir tercihi → otelin varsayılan dili. Fatura dili belge üzerinde
    /// donar (sonradan değişmemesi gerekir), bu yüzden oluşturma anında karara bağlanır.
    /// </summary>
    private static string ResolveCulture(string? requested, string? guestCulture, string hotelDefault)
    {
        if (SupportedCultures.IsSupported(requested))
        {
            return SupportedCultures.Normalize(requested!);
        }

        if (SupportedCultures.IsSupported(guestCulture))
        {
            return SupportedCultures.Normalize(guestCulture!);
        }

        return SupportedCultures.IsSupported(hotelDefault)
            ? SupportedCultures.Normalize(hotelDefault)
            : SupportedCultures.Default;
    }
}
