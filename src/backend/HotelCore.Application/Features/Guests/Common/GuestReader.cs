using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Models;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Guests.Common;

/// <summary>
/// Misafir yanıtlarının tek üretim noktası (liste, detay ve yazma uçlarının gövdesi).
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir;
/// burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ.
/// </para>
/// </summary>
internal sealed class GuestReader(IAppDbContext database, IDateTimeProvider clock)
{
    public async Task<PagedResult<GuestResponse>> ListAsync(
        GuestListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = ApplySearch(database.Guests, query.Search);

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await filtered
            // Ad sirasi: soyad, ad, sonra Id (sayfalama esitlikte kararli kalsin).
            .OrderBy(guest => guest.LastName)
            .ThenBy(guest => guest.FirstName)
            .ThenBy(guest => guest.Id)
            .Skip(query.Paging.Skip)
            .Take(query.Paging.PageSize)
            .Project()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // stayCount liste yanitinda dondurulmez (satir basina korele alt sorgu maliyeti).
        var items = rows.ConvertAll(row => ToResponse(row, stayCount: null));

        return new PagedResult<GuestResponse>(
            items,
            query.Paging.Page,
            query.Paging.PageSize,
            totalCount);
    }

    /// <summary>
    /// Tek misafir; bulunamazsa (veya başka otele aitse) 404. <c>stayCount</c> yalnızca burada
    /// hesaplanır: tamamlanmış (<c>CheckedOut</c>) konaklamaların sayısı.
    /// </summary>
    public async Task<GuestResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await database.Guests
            .Where(candidate => candidate.Id == id)
            .Project()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Guest), id);

        var stayCount = await database.Reservations
            .CountAsync(
                reservation => reservation.GuestId == id
                               && reservation.Status == ReservationStatus.CheckedOut,
                cancellationToken)
            .ConfigureAwait(false);

        return ToResponse(row, stayCount);
    }

    /// <summary>Yazma uçları için izlenen (tracked) misafir; bulunamazsa 404.</summary>
    public async Task<Guest> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var guest = await database.Guests
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return guest ?? throw new NotFoundException(nameof(Guest), id);
    }

    /// <summary>
    /// Silme ön koşulu: misafirin <b>aktif veya gelecek</b> rezervasyonu olmamalıdır.
    /// <list type="bullet">
    ///   <item>otelde konaklayan (<c>CheckedIn</c>) misafir silinemez — tarih ne olursa olsun,</item>
    ///   <item>çıkışı bugün veya sonrası olan (<c>CheckOut &gt;= bugün</c>) iptal edilmemiş
    ///         rezervasyonu olan misafir silinemez.</item>
    /// </list>
    /// Geçmiş konaklamalar silmeyi engellemez; kayıt soft-delete olduğu için tarihçe korunur.
    /// </summary>
    public async Task EnsureDeletableAsync(Guid guestId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        var hasActiveReservations = await database.Reservations
            .AnyAsync(
                reservation => reservation.GuestId == guestId
                               && (reservation.Status == ReservationStatus.CheckedIn
                                   || (reservation.CheckOut >= today
                                       && reservation.Status != ReservationStatus.Cancelled
                                       && reservation.Status != ReservationStatus.NoShow
                                       && reservation.Status != ReservationStatus.CheckedOut)),
                cancellationToken)
            .ConfigureAwait(false);

        if (hasActiveReservations)
        {
            throw new ConflictException(
                "Bu misafirin aktif veya gelecek tarihli rezervasyonu var; once rezervasyonlari iptal edin.");
        }
    }

    private static GuestResponse ToResponse(GuestRow row, int? stayCount) =>
        new()
        {
            Id = row.Id,
            FirstName = row.FirstName,
            LastName = row.LastName,
            FullName = row.FirstName + " " + row.LastName,
            Email = row.Email,
            Phone = row.Phone,
            Nationality = row.Nationality?.ToString(),
            AddressLine = row.AddressLine,
            PostalCode = row.PostalCode,
            City = row.City,
            BirthDate = row.BirthDate,
            Culture = row.Culture,
            Note = row.Note,
            StayCount = stayCount,
        };

    private static IQueryable<Guest> ApplySearch(IQueryable<Guest> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        // Büyük/küçük harf duyarsız "contains": terim C# tarafında küçültülür, kolonlar SQL'de
        // lower(...) ile küçültülür (oda/personel modülleriyle aynı desen).
        var term = search.Trim().ToLowerInvariant();

        // CA1304/CA1311/CA1862 bastırılır: kültür parametreli aşırı yüklemeleri EF Core SQL'e
        // çeviremez; parametresiz ToLower() PostgreSQL'de lower(...) olur.
#pragma warning disable CA1304, CA1311, CA1862
        return query.Where(guest =>
            guest.FirstName.ToLower().Contains(term)
            || guest.LastName.ToLower().Contains(term)
            || (guest.Email != null && guest.Email.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
    }
}

/// <summary>Misafir izdüşümü — yalnızca yanıt için gereken kolonlar okunur.</summary>
internal static class GuestQueryExtensions
{
    public static IQueryable<GuestRow> Project(this IQueryable<Guest> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(guest => new GuestRow(
            guest.Id,
            guest.FirstName,
            guest.LastName,
            guest.Email,
            guest.Phone,
            guest.Nationality,
            guest.AddressLine,
            guest.PostalCode,
            guest.City,
            guest.BirthDate,
            guest.Culture,
            guest.Note));
    }
}
