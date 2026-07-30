using System.Globalization;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura satırlarını üretir: (a) elle girilen satırlardan, (b) rezervasyon + folio'dan.
/// Tutarlar ve KDV oranları <b>her zaman</b> burada (sunucuda) hesaplanır — bkz.
/// <see cref="InvoiceAmounts"/>.
/// </summary>
internal sealed class InvoiceLineComposer(IAppDbContext database)
{
    /// <summary>Elle girilen satırları entity'ye çevirir.</summary>
    public static List<InvoiceLineItem> BuildManualLines(
        Guid hotelId,
        InvoiceTaxContext tax,
        IReadOnlyList<InvoiceLineInput> inputs,
        DateOnly fallbackServiceDate)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var lines = new List<InvoiceLineItem>(inputs.Count);
        var sortOrder = 0;

        foreach (var input in inputs)
        {
            var line = new InvoiceLineItem
            {
                HotelId = hotelId,
                Type = input.Type,
                Description = input.Description.Trim(),
                Quantity = InvoiceAmounts.Round(input.Quantity),
                UnitPrice = InvoiceAmounts.Round(input.UnitPrice),
                // Leistungsdatum (GoBD): verilmezse fatura gunu.
                ServiceDate = input.ServiceDate ?? fallbackServiceDate,
                SortOrder = sortOrder++
            };

            // KDV orani ISTEMCIDEN ALINMAZ: satir turune gore otelin vergi profilinden cozulur.
            InvoiceAmounts.ApplyLineAmounts(line, InvoiceAmounts.ResolveVatRate(line.Type, tax));

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// Rezervasyondan fatura satırları üretir:
    /// <list type="number">
    ///   <item><b>Oda ücreti:</b> gece sayısı × gecelik fiyat. Fiyat kaynağı sırayla
    ///   <c>RatePlan.Price</c> → <c>RoomType.BasePrice</c>'dır. <c>Reservation.TotalAmount</c>
    ///   bilinçli olarak kullanılmaz: ekstraları/indirimleri içerebilir ve faturada hangi kalemin
    ///   ne olduğunun görünmesi gerekir (GoBD kalem bazlı belge).</item>
    ///   <item><b>Ekstralar:</b> folio'nun henüz faturalanmamış satırları
    ///   (<c>FolioId = folio ve InvoiceId = null</c>) faturaya <b>taşınır</b> (Domain tasarımı:
    ///   satır hem folio'yu hem faturayı işaret eder). Böylece aynı masraf iki kez faturalanamaz.</item>
    ///   <item><b>Kurtaxe:</b> otelde etkinse (kişi × gece) × <c>CityTaxPerPersonNight</c>,
    ///   <c>Type = CityTax</c> ve <b>KDV'siz</b>.</item>
    /// </list>
    /// </summary>
    public async Task<ReservationCharges> BuildFromReservationAsync(
        Guid reservationId,
        InvoiceTaxContext tax,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tax);

        var source = await database.Reservations
            .Where(reservation => reservation.Id == reservationId)
            .Select(reservation => new ReservationChargeSource(
                reservation.Id,
                reservation.HotelId,
                reservation.GuestId,
                reservation.ReservationNumber,
                reservation.CheckIn,
                reservation.CheckOut,
                reservation.Adults,
                reservation.Children,
                reservation.Room.Number,
                reservation.Room.RoomType.Code,
                reservation.RatePlan == null ? null : (decimal?)reservation.RatePlan.Price,
                reservation.Room.RoomType.BasePrice,
                reservation.Folio == null ? null : (Guid?)reservation.Folio.Id))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Rezervasyon bulunamadi.");

        // Ayni faturanin iki kez uretilmesini engelle: iptal edilmemis bir faturasi varsa 409.
        var alreadyInvoiced = await database.Invoices
            .AnyAsync(
                invoice => invoice.ReservationId == reservationId
                           && invoice.Status != InvoiceStatus.Cancelled,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyInvoiced)
        {
            throw new ConflictException(
                "Bu rezervasyon icin zaten iptal edilmemis bir fatura var. " +
                "Yeni fatura kesmek icin oncekini iptal edin (Stornorechnung).");
        }

        var nights = Math.Max(1, source.CheckOut.DayNumber - source.CheckIn.DayNumber);
        var nightlyPrice = source.RatePlanPrice ?? source.BasePrice;
        var persons = Math.Max(1, source.Adults + source.Children);

        var lines = new List<InvoiceLineItem>(2);

        var roomCharge = new InvoiceLineItem
        {
            HotelId = source.HotelId,
            Type = InvoiceLineType.RoomCharge,
            Description = string.Format(
                CultureInfo.InvariantCulture,
                "Room charge {0:yyyy-MM-dd} - {1:yyyy-MM-dd} ({2} x night, room {3}/{4})",
                source.CheckIn,
                source.CheckOut,
                nights,
                source.RoomNumber,
                source.RoomTypeCode),
            Quantity = nights,
            UnitPrice = InvoiceAmounts.Round(nightlyPrice),
            ServiceDate = source.CheckIn,
            SortOrder = 0
        };

        InvoiceAmounts.ApplyLineAmounts(
            roomCharge,
            InvoiceAmounts.ResolveVatRate(InvoiceLineType.RoomCharge, tax));

        lines.Add(roomCharge);

        // Folio'daki (henuz faturalanmamis) ekstralar.
        var folioLines = source.FolioId is Guid folioId
            ? await database.InvoiceLineItems
                .Where(line => line.FolioId == folioId && line.InvoiceId == null)
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        var sortOrder = 1;
        foreach (var folioLine in folioLines)
        {
            folioLine.SortOrder = sortOrder++;

            // Folio satirlari baska bir modulden gelir; tutarlari BURADA yeniden hesaplanir.
            // Satirda anlamli bir oran varsa (0'dan buyuk) korunur — orn. ozel oranli bir kalem;
            // yoksa satir turunden cozulur.
            var vatRate = folioLine.Type is InvoiceLineType.CityTax
                ? 0m
                : folioLine.VatRate > 0m
                    ? folioLine.VatRate
                    : InvoiceAmounts.ResolveVatRate(folioLine.Type, tax);

            InvoiceAmounts.ApplyLineAmounts(folioLine, vatRate);
        }

        // Kurtaxe (City Tax) — otelde etkinse. KDV'ye TABI DEGILDIR (bkz. InvoiceAmounts §3).
        if (tax.CityTaxEnabled && tax.CityTaxPerPersonNight > 0m)
        {
            var cityTax = new InvoiceLineItem
            {
                HotelId = source.HotelId,
                Type = InvoiceLineType.CityTax,
                Description = string.Format(
                    CultureInfo.InvariantCulture,
                    "City tax (Kurtaxe) {0} person(s) x {1} night(s)",
                    persons,
                    nights),
                Quantity = persons * nights,
                UnitPrice = InvoiceAmounts.Round(tax.CityTaxPerPersonNight),
                ServiceDate = source.CheckIn,
                SortOrder = sortOrder
            };

            InvoiceAmounts.ApplyLineAmounts(cityTax, 0m);

            lines.Add(cityTax);
        }

        return new ReservationCharges(source, lines, folioLines);
    }

    /// <summary>
    /// Folio kaynaklı satırları faturadan <b>koparır</b> (silmez): <c>InvoiceId = null</c> ile
    /// masraf folio'da kalır ve ileride yeniden faturalanabilir. Taslak iptalinde ve satır
    /// değişiminde kullanılır — aksi hâlde folio masrafları kaybolurdu.
    /// </summary>
    public static void ReleaseFolioLines(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        foreach (var line in invoice.LineItems.Where(line => line.FolioId is not null).ToList())
        {
            line.InvoiceId = null;
            line.Invoice = null;
            invoice.LineItems.Remove(line);
        }
    }

    /// <summary>
    /// Faturaya özgü (folio kaynaklı olmayan) satırları siler — PUT'un "tam değişim" semantiği.
    /// Yalnızca taslak faturada çağrılır; kesinleşmiş faturada <c>AppDbContext</c> guard'ı zaten
    /// reddeder.
    /// </summary>
    public void RemoveOwnLines(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        foreach (var line in invoice.LineItems.Where(line => line.FolioId is null).ToList())
        {
            database.InvoiceLineItems.Remove(line);
            invoice.LineItems.Remove(line);
        }
    }
}

/// <summary>Rezervasyondan üretilen fatura girdisi.</summary>
/// <param name="Source">Rezervasyon özeti (misafir, otel, tarihler).</param>
/// <param name="NewLines">Faturaya eklenecek yeni satırlar (oda ücreti + Kurtaxe).</param>
/// <param name="FolioLines">Faturaya taşınacak mevcut folio satırları.</param>
internal sealed record ReservationCharges(
    ReservationChargeSource Source,
    List<InvoiceLineItem> NewLines,
    List<InvoiceLineItem> FolioLines);

/// <summary>Faturalama için gereken rezervasyon alanları (yalnızca bu kolonlar okunur).</summary>
internal sealed record ReservationChargeSource(
    Guid Id,
    Guid HotelId,
    Guid GuestId,
    string ReservationNumber,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    string RoomNumber,
    string RoomTypeCode,
    decimal? RatePlanPrice,
    decimal BasePrice,
    Guid? FolioId);
