using FluentValidation;
using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Features.Reservations.Common;

/// <summary>
/// Rezervasyon yazma kuralları tek yerde — api-contracts-reservations.md → "Reservations".
/// <para>
/// Burada yalnızca <b>istekten bağımsız</b> doğrulanabilenler vardır. İş kuralları handler'da:
/// oda/misafir aktif otelde mi (404), oda müsait mi (409), oda tipi kapasitesi (400),
/// durum geçişi (409). Tutar hiçbir koşulda istekten okunmaz.
/// </para>
/// </summary>
/// <typeparam name="TRequest">Create veya Update isteği.</typeparam>
public abstract class ReservationWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IReservationWriteRequest
{
    private const int MaxNotesLength = 1000;

    /// <summary>Tek rezervasyonda izin verilen en fazla gece sayısı.</summary>
    private const int MaxNights = 365;

    private const int MaxAdults = 20;

    private const int MaxChildren = 20;

    protected ReservationWriteValidator()
    {
        RuleFor(request => request.RoomId).NotEmpty();
        RuleFor(request => request.GuestId).NotEmpty();
        RuleFor(request => request.CheckIn).NotEmpty();
        RuleFor(request => request.CheckOut).NotEmpty();
        RuleFor(request => request.Adults).InclusiveBetween(1, MaxAdults);
        RuleFor(request => request.Children).InclusiveBetween(0, MaxChildren);
        RuleFor(request => request.Channel).IsInEnum();
        RuleFor(request => request.DepositPercent).InclusiveBetween(0m, 100m);
        RuleFor(request => request.Notes).MaximumLength(MaxNotesLength);

        // Yari acik aralik [CheckIn, CheckOut): en az bir gece olmalidir, yani CheckOut > CheckIn.
        // Ayni gun cikis (0 gece) day-use satisidir ve bu fazda desteklenmez.
        RuleFor(request => request.CheckOut)
            .GreaterThan(request => request.CheckIn)
            .WithMessage(_ => Messages.CheckOutAfterCheckIn);

        RuleFor(request => request.CheckOut)
            .Must((request, checkOut) => checkOut.DayNumber - request.CheckIn.DayNumber <= MaxNights)
            .WithMessage(_ => Messages.MaxNights(MaxNights))
            .When(request => request.CheckOut > request.CheckIn);
    }
}
