using FluentValidation;

namespace HotelCore.Application.Features.RatePlans.Common;

/// <summary>
/// Fiyat planı yazma kuralları tek yerde — api-contracts-reservations.md → "Rate Plans".
/// Oda tipinin aktif otelde olması (404) ve <b>tarih aralığı çakışması</b> (409) handler'da
/// kontrol edilir; buradaki kurallar istekten bağımsız olarak doğrulanabilenlerdir.
/// </summary>
/// <typeparam name="TRequest">Create veya Update isteği.</typeparam>
public abstract class RatePlanWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IRatePlanWriteRequest
{
    private const int MaxNameLength = 150;

    /// <summary>Fiyatta üst sınır: veri girişi hatasını (kuruş/cent karışması) erken yakalar.</summary>
    private const decimal MaxPrice = 100_000m;

    protected RatePlanWriteValidator()
    {
        RuleFor(request => request.RoomTypeId).NotEmpty();
        RuleFor(request => request.Name).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(request => request.Price).InclusiveBetween(0m, MaxPrice);
        RuleFor(request => request.ValidFrom).NotEmpty();
        RuleFor(request => request.ValidTo).NotEmpty();

        // Kapalı aralık: tek günlük plan icin ValidTo == ValidFrom gecerlidir.
        RuleFor(request => request.ValidTo)
            .GreaterThanOrEqualTo(request => request.ValidFrom)
            .WithMessage("'validTo' tarihi 'validFrom' tarihinden once olamaz.");

        RuleFor(request => request.Channel)
            .IsInEnum()
            .When(request => request.Channel is not null);
    }
}
