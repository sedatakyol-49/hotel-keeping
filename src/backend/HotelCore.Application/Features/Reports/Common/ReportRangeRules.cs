using FluentValidation;

namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// İki rapor ucunun <b>ortak</b> tarih aralığı kuralları. Tek yerde tutulur ki doluluk ve ciro
/// raporu aynı sınırlara ve aynı hata mesajlarına sahip olsun (aynı ekranda yan yana
/// kullanılacaklar).
/// </summary>
internal interface IReportRangeRequest
{
    /// <summary>Aralığın ilk günü (dâhil).</summary>
    DateOnly From { get; }

    /// <summary>Aralığın son günü (<b>dâhil</b>).</summary>
    DateOnly To { get; }
}

/// <summary>
/// <c>to &gt;= from</c> (tek günlük rapor geçerlidir — doluluk grid'inden farklı olarak burada
/// birim gün'dür) ve aralık en fazla <see cref="ReportDefinitions.MaxRangeDays"/> gün.
/// Sınır aşılınca <b>400</b>; sessizce kırpılmaz.
/// </summary>
internal static class ReportRangeRules
{
    public static void Apply<TRequest>(AbstractValidator<TRequest> validator)
        where TRequest : IReportRangeRequest
    {
        ArgumentNullException.ThrowIfNull(validator);

        validator.RuleFor(request => request.From).NotEmpty();
        validator.RuleFor(request => request.To).NotEmpty();

        validator.RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From)
            .WithMessage("'to' tarihi 'from' tarihinden once olamaz (tek gunluk rapor icin esit olabilir).");

        validator.RuleFor(request => request.To)
            .Must((request, to) =>
                to.DayNumber - request.From.DayNumber + 1 <= ReportDefinitions.MaxRangeDays)
            .WithMessage(
                $"Rapor araligi en fazla {ReportDefinitions.MaxRangeDays} gun olabilir; " +
                "daha uzun donemler icin araligi bolerek sorgulayin.")
            .When(request => request.To >= request.From);
    }
}
