using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Common.Services;
using HotelCore.Application.Features.Rooms.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Availability.Common;

/// <summary>
/// Müsaitlik ve doluluk yanıtlarının tek üretim noktası.
/// <para>
/// Her iki uç da <b>aktif otel gerektirir</b> (<c>ICurrentUser.RequireHotelId()</c>): oda × gün
/// matrisi ve müsait oda listesi tek bir otele aittir. Head Office kullanıcısı <c>X-Hotel-Id</c>
/// göndermezse konsolide moddadır ve "hangi otelin takvimi" belirsiz olur → 400.
/// </para>
/// <para>
/// Çakışma mantığı <see cref="IAvailabilityService"/> / <see cref="AvailabilityQuery"/>
/// içindedir; burada tekrar edilmez.
/// </para>
/// </summary>
internal sealed class AvailabilityReader(
    IAppDbContext database,
    ICurrentUser currentUser,
    IAvailabilityService availability)
{
    public async Task<AvailabilityResponse> GetAvailabilityAsync(
        DateOnly from,
        DateOnly to,
        Guid? roomTypeId,
        CancellationToken cancellationToken)
    {
        var hotelId = currentUser.RequireHotelId();

        var availableIds = await availability
            .GetAvailableRoomIdsAsync(from, to, roomTypeId, cancellationToken)
            .ConfigureAwait(false);

        var scope = database.Rooms.Where(room => room.HotelId == hotelId);

        if (roomTypeId is Guid typeId)
        {
            scope = scope.Where(room => room.RoomTypeId == typeId);
        }

        var rooms = await scope
            .OrderByFloorThenNumber()
            .Select(room => new AvailabilityRoomProjection(
                room.Id,
                room.Number,
                room.Floor,
                room.RoomTypeId,
                room.RoomType.Code ?? string.Empty,
                room.RoomType.Capacity,
                room.IsOutOfOrder))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var availableIdSet = availableIds.ToHashSet();

        var availableRooms = rooms
            .Where(room => availableIdSet.Contains(room.Id))
            .Select(room => new AvailableRoomDto
            {
                RoomId = room.Id,
                RoomNumber = room.Number,
                Floor = room.Floor,
                RoomTypeId = room.RoomTypeId,
                RoomTypeCode = room.RoomTypeCode,
                Capacity = room.Capacity,
            })
            .ToList();

        var byRoomType = availableRooms
            .GroupBy(room => (room.RoomTypeId, room.RoomTypeCode))
            .Select(group => new AvailabilityByRoomTypeDto(
                group.Key.RoomTypeId,
                group.Key.RoomTypeCode,
                group.Count()))
            .OrderBy(item => item.RoomTypeCode, StringComparer.Ordinal)
            .ToList();

        return new AvailabilityResponse
        {
            From = from,
            To = to,
            Nights = to.DayNumber - from.DayNumber,
            RoomTypeId = roomTypeId,
            TotalRoomCount = rooms.Count,
            OutOfOrderRoomCount = rooms.Count(room => room.IsOutOfOrder),
            AvailableRoomCount = availableRooms.Count,
            ByRoomType = byRoomType,
            Rooms = availableRooms,
        };
    }

    /// <summary>
    /// Doluluk grid'i: odalar (satır) × geceler (kolon). Yalnızca <b>iki sorgu</b> çalışır
    /// (odalar + aralıkla kesişen rezervasyonlar); hücreler bellekte tek geçişte kurulur.
    /// </summary>
    public async Task<OccupancyResponse> GetOccupancyAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var hotelId = currentUser.RequireHotelId();

        var rooms = await database.Rooms
            .Where(room => room.HotelId == hotelId)
            .OrderByFloorThenNumber()
            .Select(room => new AvailabilityRoomProjection(
                room.Id,
                room.Number,
                room.Floor,
                room.RoomTypeId,
                room.RoomType.Code ?? string.Empty,
                room.RoomType.Capacity,
                room.IsOutOfOrder))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Yari acik aralik: [from, to) ile kesisen ve odayi bloke eden rezervasyonlar
        // (Cancelled/NoShow haric — bkz. AvailabilityQuery).
        var stays = await database.Reservations
            .Where(reservation => reservation.HotelId == hotelId)
            .BlockingBetween(from, to)
            .Select(reservation => new OccupancyStayProjection(
                reservation.Id,
                reservation.ReservationNumber,
                reservation.RoomId,
                reservation.Guest.FirstName + " " + reservation.Guest.LastName,
                reservation.Status.ToString(),
                reservation.CheckIn,
                reservation.CheckOut))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var days = BuildDays(from, to);
        var cellsByRoom = BuildCells(stays, from, to);

        var rows = rooms.ConvertAll(room => new OccupancyRoomRowDto
        {
            RoomId = room.Id,
            RoomNumber = room.Number,
            Floor = room.Floor,
            RoomTypeId = room.RoomTypeId,
            RoomTypeCode = room.RoomTypeCode,
            IsOutOfOrder = room.IsOutOfOrder,
            Cells = cellsByRoom.TryGetValue(room.Id, out var cells) ? cells : [],
        });

        var occupied = rows.Sum(row => row.Cells.Count);
        var roomNights = rooms.Count * days.Count;

        return new OccupancyResponse
        {
            From = from,
            To = to,
            Days = days,
            Rooms = rows,
            Summary = new OccupancySummaryDto(
                rooms.Count,
                days.Count,
                roomNights,
                occupied,
                roomNights == 0
                    ? 0m
                    : Math.Round(occupied * 100m / roomNights, 2, MidpointRounding.AwayFromZero)),
        };
    }

    private static List<DateOnly> BuildDays(DateOnly from, DateOnly to)
    {
        var days = new List<DateOnly>(Math.Max(to.DayNumber - from.DayNumber, 0));

        for (var day = from; day < to; day = day.AddDays(1))
        {
            days.Add(day);
        }

        return days;
    }

    /// <summary>
    /// Rezervasyonları oda bazında hücrelere açar. Bir konaklamanın hücreleri
    /// <c>[max(CheckIn, from), min(CheckOut, to))</c> aralığındaki gecelerdir — çıkış günü
    /// (<c>CheckOut</c>) hücre üretmez, çünkü o gece oda boştur ve aynı gün başka bir misafire
    /// satılabilir.
    /// </summary>
    private static Dictionary<Guid, List<OccupancyCellDto>> BuildCells(
        List<OccupancyStayProjection> stays,
        DateOnly from,
        DateOnly to)
    {
        var result = new Dictionary<Guid, List<OccupancyCellDto>>();

        foreach (var stay in stays)
        {
            var start = stay.CheckIn > from ? stay.CheckIn : from;
            var end = stay.CheckOut < to ? stay.CheckOut : to;
            var lastNight = stay.CheckOut.AddDays(-1);

            for (var night = start; night < end; night = night.AddDays(1))
            {
                if (!result.TryGetValue(stay.RoomId, out var cells))
                {
                    cells = [];
                    result[stay.RoomId] = cells;
                }

                cells.Add(new OccupancyCellDto
                {
                    Date = night,
                    ReservationId = stay.Id,
                    ReservationNumber = stay.ReservationNumber,
                    GuestName = stay.GuestName,
                    Status = stay.Status,
                    IsArrival = night == stay.CheckIn,
                    IsDeparture = night == lastNight,
                });
            }
        }

        foreach (var cells in result.Values)
        {
            cells.Sort((left, right) => left.Date.CompareTo(right.Date));
        }

        return result;
    }

    /// <summary>Oda satırının ham izdüşümü (iki uçta paylaşılır).</summary>
    private sealed record AvailabilityRoomProjection(
        Guid Id,
        string Number,
        int Floor,
        Guid RoomTypeId,
        string RoomTypeCode,
        int Capacity,
        bool IsOutOfOrder);

    /// <summary>Grid'e açılacak konaklamanın ham izdüşümü.</summary>
    private sealed record OccupancyStayProjection(
        Guid Id,
        string ReservationNumber,
        Guid RoomId,
        string GuestName,
        string Status,
        DateOnly CheckIn,
        DateOnly CheckOut);
}
