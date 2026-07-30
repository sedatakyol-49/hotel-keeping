using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Models;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura okuma yolunun tek üretim noktası: liste, detay ve yazma uçlarının döndürdüğü gövde
/// buradan gelir.
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir; burada
/// <c>HotelId</c>/<c>IsDeleted</c> koşulu <b>yazılmaz</b>. Başka otelin faturası "bulunamadı"
/// (404) olur — varlığı sızdırılmaz.
/// </para>
/// </summary>
internal sealed class InvoiceReader(IAppDbContext database)
{
    public async Task<PagedResult<InvoiceResponse>> ListAsync(
        InvoiceListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = ApplyFilters(database.Invoices, query);

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await filtered
            // Siralama: fatura tarihi tersine; taslakta tarih olmadigi icin olusturma tarihine
            // dusulur (COALESCE) -> taslaklar zaman cizgisinde dogru yere oturur.
            .OrderByDescending(invoice => invoice.IssuedAt ?? invoice.CreatedAt)
            .ThenBy(invoice => invoice.Id)
            .Skip(query.Paging.Skip)
            .Take(query.Paging.PageSize)
            .Select(invoice => new InvoiceResponse
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber == string.Empty ? null : invoice.InvoiceNumber,
                Status = invoice.Status.ToString(),
                IssuedAt = invoice.IssuedAt,
                GuestId = invoice.GuestId,
                GuestName = invoice.Guest.FirstName + " " + invoice.Guest.LastName,
                ReservationId = invoice.ReservationId,
                ReservationNumber = invoice.Reservation != null ? invoice.Reservation.ReservationNumber : null,
                Culture = invoice.Culture,
                Currency = invoice.Currency,
                NetAmount = invoice.NetAmount,
                VatAmount = invoice.VatAmount,
                CityTaxAmount = invoice.CityTaxAmount,
                GrossAmount = invoice.GrossAmount,
                PaidAmount = invoice.Payments.Sum(payment => (decimal?)payment.Amount) ?? 0m,
                OutstandingAmount =
                    invoice.GrossAmount - (invoice.Payments.Sum(payment => (decimal?)payment.Amount) ?? 0m),
                CancelledByInvoiceId = invoice.CancelledByInvoiceId,
                // Ters bag DOGRUDAN kolondan okunur (Invoice.CancelsInvoiceId): storno cifti
                // domain'de her iki yonden kurulur (MarkCancelled(Invoice)), bu yuzden burada
                // ilintili alt sorgu (her satir icin Invoices taramasi) YOK.
                CancelsInvoiceId = invoice.CancelsInvoiceId,
                CreatedAt = invoice.CreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<InvoiceResponse>(
            // Turetilmis bayrak: SQL'e cevrilebilir bir ifade degil, bu yuzden bellekte doldurulur.
            items.ConvertAll(item => item with { IsCancellationInvoice = item.CancelsInvoiceId is not null }),
            query.Paging.Page,
            query.Paging.PageSize,
            totalCount);
    }

    /// <summary>
    /// Fatura detayı: satırlar + ödemeler + denetim izi. Bulunamazsa (veya başka otele aitse) 404.
    /// </summary>
    public async Task<InvoiceDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await database.Invoices
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new InvoiceDetailResponse
            {
                Id = candidate.Id,
                InvoiceNumber = candidate.InvoiceNumber == string.Empty ? null : candidate.InvoiceNumber,
                Status = candidate.Status.ToString(),
                IssuedAt = candidate.IssuedAt,
                GuestId = candidate.GuestId,
                GuestName = candidate.Guest.FirstName + " " + candidate.Guest.LastName,
                ReservationId = candidate.ReservationId,
                ReservationNumber = candidate.Reservation != null ? candidate.Reservation.ReservationNumber : null,
                Culture = candidate.Culture,
                Currency = candidate.Currency,
                NetAmount = candidate.NetAmount,
                VatAmount = candidate.VatAmount,
                CityTaxAmount = candidate.CityTaxAmount,
                GrossAmount = candidate.GrossAmount,
                PaidAmount = candidate.Payments.Sum(payment => (decimal?)payment.Amount) ?? 0m,
                OutstandingAmount =
                    candidate.GrossAmount - (candidate.Payments.Sum(payment => (decimal?)payment.Amount) ?? 0m),
                CancelledByInvoiceId = candidate.CancelledByInvoiceId,
                // Ters bag dogrudan kolondan (bkz. ListAsync notu).
                CancelsInvoiceId = candidate.CancelsInvoiceId,
                CreatedAt = candidate.CreatedAt,
                LineItems = candidate.LineItems
                    .OrderBy(line => line.SortOrder)
                    .ThenBy(line => line.Id)
                    .Select(line => new InvoiceLineItemResponse
                    {
                        Id = line.Id,
                        Type = line.Type.ToString(),
                        Description = line.Description,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        VatRate = line.VatRate,
                        LineNet = line.LineNet,
                        LineVat = line.LineVat,
                        LineGross = line.LineNet + line.LineVat,
                        ServiceDate = line.ServiceDate,
                        SortOrder = line.SortOrder,
                    })
                    .ToList(),
                Payments = candidate.Payments
                    .OrderBy(payment => payment.PaidAt)
                    .ThenBy(payment => payment.Id)
                    .Select(payment => new InvoicePaymentResponse
                    {
                        Id = payment.Id,
                        Method = payment.Method.ToString(),
                        Amount = payment.Amount,
                        PaidAt = payment.PaidAt,
                        Reference = payment.Reference,
                    })
                    .ToList(),
                AuditTrail = candidate.AuditEntries
                    .OrderBy(entry => entry.PerformedAt)
                    .ThenBy(entry => entry.Id)
                    .Select(entry => new InvoiceAuditEntryResponse
                    {
                        Id = entry.Id,
                        Action = entry.Action.ToString(),
                        PerformedByUserId = entry.PerformedByUserId,
                        PerformedAt = entry.PerformedAt,
                        Details = entry.Details,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            throw new NotFoundException(nameof(Invoice), id);
        }

        return invoice with { IsCancellationInvoice = invoice.CancelsInvoiceId is not null };
    }

    /// <summary>
    /// Yazma yolu için izlenen (tracked) fatura. Satırlar <c>Include</c> ile yüklenir çünkü
    /// tutarlar her yazma işleminde satırlardan <b>yeniden</b> hesaplanır.
    /// </summary>
    public async Task<Invoice> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await database.Invoices
            .Include(candidate => candidate.LineItems)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return invoice ?? throw new NotFoundException(nameof(Invoice), id);
    }

    /// <summary>
    /// Faturanın oteli + vergi profili. Otel tenant-scoped değildir, bu yüzden kimliğe göre
    /// doğrudan okunur; erişim denetimi <c>X-Hotel-Id</c> middleware'i ve faturanın kendi
    /// tenant filtresiyle zaten yapılmıştır.
    /// </summary>
    public async Task<InvoiceTaxContext> GetTaxContextAsync(Guid hotelId, CancellationToken cancellationToken)
    {
        var context = await database.Hotels
            .Where(hotel => hotel.Id == hotelId)
            .Select(hotel => new InvoiceTaxContext(
                hotel.Id,
                hotel.Currency,
                hotel.DefaultCulture,
                hotel.TaxProfile.VatRate,
                hotel.TaxProfile.ReducedVatRate,
                hotel.TaxProfile.CityTaxPerPersonNight,
                hotel.TaxProfile.CityTaxEnabled,
                hotel.TaxProfile.CityTaxExemptChildren,
                hotel.TaxProfile.CityTaxChildAgeLimit))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return context ?? throw new NotFoundException("Otel bulunamadi.");
    }

    /// <summary>Misafirin aktif otelde var olduğunu doğrular; yoksa 404 (tenant sızıntısı yok).</summary>
    public async Task<Guest> GetGuestAsync(Guid guestId, CancellationToken cancellationToken)
    {
        var guest = await database.Guests
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == guestId, cancellationToken)
            .ConfigureAwait(false);

        return guest ?? throw new NotFoundException("Misafir bulunamadi.");
    }

    /// <summary>Faturaya yapılmış ödemelerin toplamı (veritabanından — istemci toplamına güvenilmez).</summary>
    public async Task<decimal> GetPaidAmountAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        await database.Payments
            .Where(payment => payment.InvoiceId == invoiceId)
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

    private static IQueryable<Invoice> ApplyFilters(IQueryable<Invoice> query, InvoiceListQuery filter)
    {
        if (filter.Status is InvoiceStatus status)
        {
            query = query.Where(invoice => invoice.Status == status);
        }

        if (filter.GuestId is Guid guestId)
        {
            query = query.Where(invoice => invoice.GuestId == guestId);
        }

        if (filter.ReservationId is Guid reservationId)
        {
            query = query.Where(invoice => invoice.ReservationId == reservationId);
        }

        if (filter.From is DateOnly from)
        {
            // Gun sinirlari UTC olarak C# tarafinda kurulur: kolon uzerinde fonksiyon
            // uygulanmadigi icin (HotelId, IssuedAt) index'i kullanilabilir kalir.
            var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(invoice => invoice.IssuedAt >= fromUtc);
        }

        if (filter.To is DateOnly to)
        {
            // Ust sinir gun DAHIL: ertesi gunun 00:00'ina kadar (haric).
            var toExclusiveUtc = new DateTimeOffset(
                to.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            query = query.Where(invoice => invoice.IssuedAt < toExclusiveUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();

            // CA1304/CA1311/CA1862 bastirilir: kultur parametreli asiri yuklemeleri EF Core
            // SQL'e cevirmez (oda/personel modullerindeki arama ile ayni gerekce).
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(invoice =>
                invoice.InvoiceNumber.ToLower().Contains(term)
                || invoice.Guest.FirstName.ToLower().Contains(term)
                || invoice.Guest.LastName.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return query;
    }
}
