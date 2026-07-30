using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Create;

/// <summary>
/// <c>POST /api/v1/reservations</c> gövdesi (rezervasyon sihirbazının son adımı).
/// <para>
/// <b>Tutar gövdede taşınmaz:</b> <c>totalAmount</c> sunucuda geçerli fiyat planı / oda tipi
/// <c>basePrice</c> üzerinden hesaplanır. İstemciden gelen bir tutar dikkate ALINMAZ.
/// </para>
/// </summary>
public sealed record CreateReservationRequest : IRequest<ReservationResponse>, IReservationWriteRequest
{
    /// <summary>Aynı otele ait oda olmalıdır (aksi hâlde 404); servis dışı olamaz (409).</summary>
    public Guid RoomId { get; init; }

    /// <summary>Aynı otele ait misafir olmalıdır; aksi hâlde 404.</summary>
    public Guid GuestId { get; init; }

    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    public int Adults { get; init; } = 1;

    public int Children { get; init; }

    public ReservationChannel Channel { get; init; } = ReservationChannel.Direct;

    public decimal DepositPercent { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Başlangıç durumu — yalnızca <c>Option</c> (opsiyon) veya <c>Confirmed</c> olabilir.
    /// Verilmezse <c>Option</c>. Diğer durumlara yalnızca ilgili aksiyon uçlarıyla geçilir
    /// (check-in / check-out / cancel / no-show).
    /// </summary>
    public ReservationStatus? Status { get; init; }
}
