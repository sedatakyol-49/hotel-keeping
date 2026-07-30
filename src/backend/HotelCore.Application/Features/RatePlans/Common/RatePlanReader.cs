using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.RatePlans.Common;

/// <summary>
/// Fiyat planı yanıtlarının tek üretim noktası + paylaşılan iş kuralları (oda tipi kapsamı,
/// tarih aralığı çakışması, silme ön koşulu).
/// <para>
/// Tenant izolasyonu <c>AppDbContext</c> global query filter'ından gelir; burada <c>HotelId</c>
/// koşulu yalnızca <b>Head Office konsolide modunda</b> yanlış otele yazmayı önlemek için
/// açıkça verilir (aktif otel bilinen yazma yollarında).
/// </para>
/// </summary>
internal sealed class RatePlanReader(IAppDbContext database)
{
    /// <summary>
    /// Fiyat planları — sayfalama yoktur (plan sayısı azdır), düz dizi döner.
    /// Sıralama: <c>validFrom</c>, sonra plan adı, sonra Id (kararlı).
    /// </summary>
    public async Task<IReadOnlyList<RatePlanResponse>> ListAsync(
        Guid? roomTypeId,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var query = database.RatePlans.AsQueryable();

        if (roomTypeId is Guid typeId)
        {
            query = query.Where(plan => plan.RoomTypeId == typeId);
        }

        if (date is DateOnly on)
        {
            // Kapali aralik: o gun gecerli olan planlar (ValidFrom <= gun <= ValidTo).
            query = query.Where(plan => plan.ValidFrom <= on && on <= plan.ValidTo);
        }

        var rows = await query
            .OrderBy(plan => plan.ValidFrom)
            .ThenBy(plan => plan.Name)
            .ThenBy(plan => plan.Id)
            .Project()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(ToResponse);
    }

    /// <summary>Tek plan; bulunamazsa (veya başka otele aitse) 404.</summary>
    public async Task<RatePlanResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await database.RatePlans
            .Where(plan => plan.Id == id)
            .Project()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(RatePlan), id);

        return ToResponse(row);
    }

    public async Task<RatePlan> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await database.RatePlans
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return plan ?? throw new NotFoundException(nameof(RatePlan), id);
    }

    /// <summary>
    /// Oda tipi aktif otelde olmalıdır; değilse 404 (başka otelin oda tipine plan bağlanamaz,
    /// varlığı da sızdırılmaz).
    /// </summary>
    public async Task EnsureRoomTypeExistsAsync(
        Guid roomTypeId,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        var exists = await database.RoomTypes
            .AnyAsync(
                roomType => roomType.Id == roomTypeId && roomType.HotelId == hotelId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new NotFoundException(nameof(RoomType), roomTypeId);
        }
    }

    /// <summary>
    /// Aynı <c>(RoomTypeId, Channel)</c> için <b>tarih aralığı çakışan</b> ikinci aktif plan
    /// engellenir (409) — aksi hâlde bir gece için iki farklı fiyat geçerli olur ve tutar
    /// belirsizleşir.
    /// <para>
    /// <b>Aralık kapalıdır</b> <c>[ValidFrom, ValidTo]</c>, bu yüzden kesişim koşulu
    /// <c>mevcut.ValidFrom &lt;= yeni.ValidTo &amp;&amp; yeni.ValidFrom &lt;= mevcut.ValidTo</c>
    /// şeklindedir (uç noktada eşitlik ÇAKIŞMADIR — rezervasyon tarihlerinin yarı açık
    /// aralığından bilinçli olarak farklı: fiyat planı "gün" kümesidir, konaklama "gece" kümesi).
    /// </para>
    /// <para>
    /// <b>Kanal karşılaştırması birebirdir:</b> kanal bazlı plan ile "tüm kanallar" planı
    /// (<c>Channel = null</c>) çakışma saymaz; belirsizlik yoktur çünkü fiyat seçiminde kanala
    /// özel plan her zaman önce gelir (bkz. <c>ReservationPricingService</c>).
    /// Pasif planlar (<c>IsActive = false</c>) çakışma üretmez.
    /// </para>
    /// </summary>
    public async Task EnsureNoOverlapAsync(
        Guid roomTypeId,
        ReservationChannel? channel,
        DateOnly validFrom,
        DateOnly validTo,
        bool isActive,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (!isActive)
        {
            return;
        }

        var conflict = await database.RatePlans
            .Where(plan => plan.RoomTypeId == roomTypeId
                           && plan.Channel == channel
                           && plan.IsActive
                           && (excludeId == null || plan.Id != excludeId)
                           && plan.ValidFrom <= validTo
                           && validFrom <= plan.ValidTo)
            .Select(plan => new { plan.Name, plan.ValidFrom, plan.ValidTo })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (conflict is not null)
        {
            var channelText = channel?.ToString() ?? "tum kanallar";

            throw new ConflictException(
                $"Bu oda tipi ve kanal ({channelText}) icin tarih araligi cakisan bir fiyat plani var: " +
                $"'{conflict.Name}' ({conflict.ValidFrom:yyyy-MM-dd} - {conflict.ValidTo:yyyy-MM-dd}). " +
                "Ayni gece icin iki fiyat gecerli olamaz.");
        }
    }

    /// <summary>
    /// Silme ön koşulu: plana bağlı rezervasyon varsa silinemez (409). <c>RatePlan</c>
    /// soft-delete edilebilir <b>değildir</b> ve <c>Reservation.RatePlanId</c> FK'si
    /// <c>Restrict</c>'tir; ön kontrol olmasa veritabanı hatası 500 olarak dönerdi.
    /// Alternatif: planı <c>isActive = false</c> yaparak pasifleştirin.
    /// </summary>
    public async Task EnsureDeletableAsync(Guid ratePlanId, CancellationToken cancellationToken)
    {
        var isUsed = await database.Reservations
            .AnyAsync(reservation => reservation.RatePlanId == ratePlanId, cancellationToken)
            .ConfigureAwait(false);

        if (isUsed)
        {
            throw new ConflictException(
                "Bu fiyat plani rezervasyonlarda kullanildigi icin silinemez; plani pasife alin (isActive = false).");
        }
    }

    private static RatePlanResponse ToResponse(RatePlanRow row) =>
        new()
        {
            Id = row.Id,
            RoomTypeId = row.RoomTypeId,
            RoomTypeCode = row.RoomTypeCode,
            RoomTypeName = row.RoomTypeName,
            Name = row.Name,
            Price = row.Price,
            Currency = row.Currency,
            ValidFrom = row.ValidFrom,
            ValidTo = row.ValidTo,
            Channel = row.Channel?.ToString(),
            IsActive = row.IsActive,
        };
}

/// <summary>Fiyat planı izdüşümü — oda tipi ve para birimi JOIN ile alınır (Include yerine).</summary>
internal static class RatePlanQueryExtensions
{
    public static IQueryable<RatePlanRow> Project(this IQueryable<RatePlan> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(plan => new RatePlanRow(
            plan.Id,
            plan.RoomTypeId,
            plan.RoomType.Code ?? string.Empty,
            plan.RoomType.Name ?? string.Empty,
            plan.Name,
            plan.Price,
            plan.Hotel.Currency ?? string.Empty,
            plan.ValidFrom,
            plan.ValidTo,
            plan.Channel,
            plan.IsActive));
    }
}
