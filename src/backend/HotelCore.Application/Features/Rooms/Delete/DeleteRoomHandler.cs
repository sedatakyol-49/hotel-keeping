using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Rooms.Delete;

/// <summary>
/// Odayı soft-delete eder.
///
/// <para><b>İki bağımsız reddetme koşulu (409) vardır:</b>
/// <list type="number">
///   <item><b>Operasyonel:</b> odanın <b>gelecek tarihli</b> (<c>CheckOut &gt;= bugün</c>) ve
///   <b>iptal edilmemiş</b> bir rezervasyonu varsa. Aksi hâlde misafiri olan bir oda listelerden
///   kaybolur ve check-in/out akışı bozulurdu.</item>
///   <item><b>Mali (GoBD / AO §147):</b> odanın <b>yürürlükteki</b> (iptal edilmemiş) ve
///   <b>henüz faturalanmamış</b> bir rezervasyonu varsa — tarihi geçmiş olsa bile.</item>
/// </list></para>
///
/// <para><b>İkinci kuralın gerekçesi (gerçek bir hatanın karşılığı).</b> Yalnızca birinci kural
/// varken, geçmiş tarihli ama faturalanmamış rezervasyonu olan oda silinebiliyordu. Rezervasyonun
/// oda navigasyonu zorunludur; oda soft-delete edilince global query filter satırı gizler, listeden
/// ve detaydan <b>404</b> dönmeye başlar ve rezervasyon <b>bir daha faturalanamaz</b> — tutarı
/// raporlarda <c>unbilledRoomRevenueGross</c> altında sonsuza kadar asılı kalır. GoBD ve <b>AO
/// §147</b> ticari kayıtların 10 yıl boyunca <i>erişilebilir</i> ve <i>makine ile
/// değerlendirilebilir</i> kalmasını ister; faturalanmamış bir konaklamayı erişilemez hâle getirmek
/// hem gelir kaybı hem kayıt bütünlüğü ihlalidir.</para>
///
/// <para><b>"Faturalanmış" ne demek:</b> tanım tek yerdedir —
/// <see cref="InvoiceEffectiveness.IsEffectiveDocument"/>: <i>iptal edilmemiş</i> +
/// <i>kendisi Stornorechnung olmayan</i> + <i>numara almış</i> (<c>IssuedAt != null</c>) fatura.
/// <b>Taslak fatura saymaz</b> (belge değildir, terk edilebilir): taslağı olan bir rezervasyonun
/// odası da silinemez — muhafazakâr olan budur.</para>
///
/// <para><b>Silme yine de mümkün:</b> engel kaldırılabilir bir durumdur — rezervasyon faturalanır
/// (veya iptal edilir), sonra oda silinir. Kayıtlar soft-delete olduğu için tarihçe her hâlükârda
/// korunur.</para>
/// </summary>
internal sealed class DeleteRoomHandler(IAppDbContext database, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeleteRoomRequest, Unit>
{
    public async Task<Unit> Handle(DeleteRoomRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await database.Rooms
            .FirstOrDefaultAsync(room => room.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), request.Id);

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var hasUpcomingReservations = await database.Reservations
            .AnyAsync(
                reservation => reservation.RoomId == entity.Id
                               && reservation.CheckOut >= today
                               && reservation.Status != ReservationStatus.Cancelled,
                cancellationToken)
            .ConfigureAwait(false);

        if (hasUpcomingReservations)
        {
            throw new ConflictException(Messages.RoomHasFutureReservations);
        }

        // Faturalanmis rezervasyonlarin kimlikleri AYRI bir IQueryable olarak kurulur ve asagida
        // Contains ile alt sorgu hâlinde kullanilir. Genisletme metodunu (EffectiveDocuments())
        // dogrudan Where/Any ifadesinin ICINE yazmak calismaz: EF Core ifade agacinda cevrilemeyen
        // bir metot cagrisi gorur. Disarida kurulan sorgu ise SQL'e sorunsuz iner.
        var billedReservationIds = database.Invoices
            .EffectiveDocuments()
            .Where(invoice => invoice.ReservationId != null)
            .Select(invoice => invoice.ReservationId!.Value);

        // GoBD / AO §147: yururlukteki ama henuz faturalanmamis konaklama erisilemez hale gelmemeli.
        // Ilk (en eski) engelleyen rezervasyon okunur; mesaj HANGI rezervasyon oldugunu soyler.
        var unbilled = await database.Reservations
            .Where(reservation => reservation.RoomId == entity.Id
                                  && reservation.Status != ReservationStatus.Cancelled
                                  && !billedReservationIds.Contains(reservation.Id))
            .OrderBy(reservation => reservation.CheckIn)
            .ThenBy(reservation => reservation.Id)
            .Select(reservation => new UnbilledReservation(
                reservation.ReservationNumber,
                reservation.CheckIn,
                reservation.CheckOut))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unbilled is not null)
        {
            throw new ConflictException(
                Messages.RoomHasUnbilledReservation(unbilled.Number, unbilled.CheckIn, unbilled.CheckOut));
        }

        database.Rooms.Remove(entity);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }

    /// <summary>Silmeyi engelleyen rezervasyonun mesaja yazılan alanları.</summary>
    private sealed record UnbilledReservation(string Number, DateOnly CheckIn, DateOnly CheckOut);
}
