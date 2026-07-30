using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Models;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Rezervasyon yanıtlarının tek üretim noktası (liste, detay, folio ve tüm yazma/durum
/// uçlarının döndürdüğü gövde).
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir;
/// burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ.
/// </para>
/// </summary>
internal sealed class ReservationReader(IAppDbContext database)
{
    public async Task<PagedResult<ReservationResponse>> ListAsync(
        ReservationListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = ApplyFilters(database.Reservations, query);

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await filtered
            // Takvim sirasi: giris gunu, sonra oda numarasi uzunlugu/numara (dogal sira),
            // sonra Id (sayfalama esitlikte kararli kalsin).
            .OrderBy(reservation => reservation.CheckIn)
            .ThenBy(reservation => reservation.Room.Number.Length)
            .ThenBy(reservation => reservation.Room.Number)
            .ThenBy(reservation => reservation.Id)
            .Skip(query.Paging.Skip)
            .Take(query.Paging.PageSize)
            .Project()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ReservationResponse>(
            rows.ConvertAll(ToResponse),
            query.Paging.Page,
            query.Paging.PageSize,
            totalCount);
    }

    /// <summary>Tek rezervasyon; bulunamazsa (veya başka otele aitse) 404.</summary>
    public async Task<ReservationResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await database.Reservations
            .Where(reservation => reservation.Id == id)
            .Project()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Reservation), id);

        return ToResponse(row);
    }

    /// <summary>Yazma/durum uçları için izlenen (tracked) rezervasyon; bulunamazsa 404.</summary>
    public async Task<Reservation> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var reservation = await database.Reservations
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return reservation ?? throw new NotFoundException(nameof(Reservation), id);
    }

    /// <summary>
    /// Folio (açık hesap) görünümü. Folio henüz açılmamışsa satır listesi boş, toplamlar sıfır
    /// döner — istemcinin ayrı bir "folio yok" durumu ele almasına gerek kalmaz.
    /// </summary>
    public async Task<FolioResponse> GetFolioAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var header = await database.Reservations
            .Where(reservation => reservation.Id == reservationId)
            .Select(reservation => new
            {
                reservation.Id,
                reservation.ReservationNumber,
                Currency = reservation.Hotel.Currency,
                GuestName = reservation.Guest.FirstName + " " + reservation.Guest.LastName,
                FolioId = (Guid?)reservation.Folio!.Id,
                FolioIsClosed = (bool?)reservation.Folio!.IsClosed,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Reservation), reservationId);

        var lines = header.FolioId is Guid folioId
            ? await database.InvoiceLineItems
                .Where(line => line.FolioId == folioId)
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.Id)
                .Select(line => new FolioLineResponse
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
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        return new FolioResponse
        {
            ReservationId = header.Id,
            ReservationNumber = header.ReservationNumber,
            FolioId = header.FolioId,
            IsClosed = header.FolioIsClosed ?? false,
            Currency = header.Currency,
            GuestName = header.GuestName,
            Lines = lines,
            TotalNet = lines.Sum(line => line.LineNet),
            TotalVat = lines.Sum(line => line.LineVat),
            TotalGross = lines.Sum(line => line.LineGross),
        };
    }

    /// <summary>
    /// Odanın aktif otelde olduğunu doğrular ve kapasite/oda tipi bilgisini döner.
    /// <paramref name="hotelId"/> koşulu Head Office konsolide modunda yanlış otelin odasına
    /// rezervasyon yazmayı önler; oda görünmüyorsa 404 (varlığı sızdırılmaz).
    /// </summary>
    public async Task<RoomBookingInfo> GetRoomForBookingAsync(
        Guid roomId,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        var room = await database.Rooms
            .Where(candidate => candidate.Id == roomId && candidate.HotelId == hotelId)
            .Select(candidate => new RoomBookingInfo(
                candidate.Id,
                candidate.Number,
                candidate.RoomTypeId,
                candidate.RoomType.Capacity,
                candidate.IsOutOfOrder))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return room ?? throw new NotFoundException(nameof(Room), roomId);
    }

    /// <summary>
    /// Misafirin aktif otelde bulunduğunu doğrular; görünmüyorsa 404 (başka otelin misafirine
    /// rezervasyon bağlanamaz, varlığı da sızdırılmaz). <paramref name="hotelId"/> koşulu
    /// Head Office konsolide modunda yanlış otelin misafirini seçmeyi önler.
    /// </summary>
    public async Task EnsureGuestExistsAsync(
        Guid guestId,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        var exists = await database.Guests
            .AnyAsync(guest => guest.Id == guestId && guest.HotelId == hotelId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new NotFoundException(nameof(Guest), guestId);
        }
    }

    /// <summary>
    /// Kişi sayısı oda tipinin kapasitesini aşamaz. Kural burada (tek yerde) uygulanır ki
    /// Create ve Update aynı davranışı göstersin.
    /// </summary>
    public static void EnsureCapacity(RoomBookingInfo room, int adults, int children)
    {
        ArgumentNullException.ThrowIfNull(room);

        var guests = adults + children;

        if (guests > room.Capacity)
        {
            throw new ValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Adults"] =
                [
                    $"'{room.Number}' numarali odanin kapasitesi {room.Capacity} kisi; {guests} kisi secildi."
                ]
            });
        }
    }

    private static ReservationResponse ToResponse(ReservationRow row) =>
        new()
        {
            Id = row.Id,
            ReservationNumber = row.ReservationNumber,
            Status = row.Status.ToString(),
            Channel = row.Channel.ToString(),
            RoomId = row.RoomId,
            RoomNumber = row.RoomNumber,
            RoomTypeId = row.RoomTypeId,
            RoomTypeCode = row.RoomTypeCode,
            GuestId = row.GuestId,
            GuestName = row.GuestFirstName + " " + row.GuestLastName,
            GuestEmail = row.GuestEmail,
            CheckIn = row.CheckIn,
            CheckOut = row.CheckOut,
            Nights = row.CheckOut.DayNumber - row.CheckIn.DayNumber,
            Adults = row.Adults,
            Children = row.Children,
            TotalAmount = row.TotalAmount,
            Currency = row.Currency,
            DepositPercent = row.DepositPercent,
            DepositAmount = Math.Round(
                row.TotalAmount * row.DepositPercent / 100m,
                2,
                MidpointRounding.AwayFromZero),
            RatePlanId = row.RatePlanId,
            RatePlanName = row.RatePlanName,
            Notes = row.Notes,
            CheckedInAt = row.CheckedInAt,
            CheckedOutAt = row.CheckedOutAt,
            FolioId = row.FolioId,
        };

    private static IQueryable<Reservation> ApplyFilters(
        IQueryable<Reservation> query,
        ReservationListQuery filter)
    {
        if (filter.Status is ReservationStatus status)
        {
            query = query.Where(reservation => reservation.Status == status);
        }

        if (filter.Channel is ReservationChannel channel)
        {
            query = query.Where(reservation => reservation.Channel == channel);
        }

        if (filter.RoomId is Guid roomId)
        {
            query = query.Where(reservation => reservation.RoomId == roomId);
        }

        if (filter.GuestId is Guid guestId)
        {
            query = query.Where(reservation => reservation.GuestId == guestId);
        }

        // Tarih araligi kesisimi yari acik aralik mantigiyla: [from, to).
        if (filter.From is DateOnly from)
        {
            query = query.Where(reservation => from < reservation.CheckOut);
        }

        if (filter.To is DateOnly to)
        {
            query = query.Where(reservation => reservation.CheckIn < to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();

            // CA1304/CA1311/CA1862 bastırılır: kültür parametreli aşırı yüklemeleri EF Core
            // SQL'e çeviremez (oda/personel modülleriyle aynı gerekçe).
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(reservation =>
                reservation.ReservationNumber.ToLower().Contains(term)
                || reservation.Guest.FirstName.ToLower().Contains(term)
                || reservation.Guest.LastName.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return query;
    }
}

/// <summary>Rezervasyon yazarken gereken oda bilgisi (kapasite kontrolü + hata mesajı için).</summary>
/// <param name="Id">Oda kimliği.</param>
/// <param name="Number">Oda numarası.</param>
/// <param name="RoomTypeId">Oda tipi kimliği.</param>
/// <param name="Capacity">Oda tipinin kişi kapasitesi.</param>
/// <param name="IsOutOfOrder">Servis dışı bayrağı.</param>
internal sealed record RoomBookingInfo(
    Guid Id,
    string Number,
    Guid RoomTypeId,
    int Capacity,
    bool IsOutOfOrder);

/// <summary>Rezervasyon izdüşümü — oda, oda tipi, misafir ve folio bilgisi JOIN ile alınır.</summary>
internal static class ReservationQueryExtensions
{
    public static IQueryable<ReservationRow> Project(this IQueryable<Reservation> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(reservation => new ReservationRow(
            reservation.Id,
            reservation.ReservationNumber,
            reservation.Status,
            reservation.Channel,
            reservation.RoomId,
            reservation.Room.Number ?? string.Empty,
            reservation.Room.RoomTypeId,
            reservation.Room.RoomType.Code ?? string.Empty,
            reservation.GuestId,
            reservation.Guest.FirstName ?? string.Empty,
            reservation.Guest.LastName ?? string.Empty,
            reservation.Guest.Email,
            reservation.CheckIn,
            reservation.CheckOut,
            reservation.Adults,
            reservation.Children,
            reservation.TotalAmount,
            reservation.Hotel.Currency ?? string.Empty,
            reservation.DepositPercent,
            reservation.RatePlanId,
            reservation.RatePlan!.Name,
            reservation.Notes,
            reservation.CheckedInAt,
            reservation.CheckedOutAt,
            (Guid?)reservation.Folio!.Id));
    }
}
