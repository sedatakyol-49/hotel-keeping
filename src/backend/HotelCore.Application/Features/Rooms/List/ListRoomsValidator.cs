using FluentValidation;

namespace HotelCore.Application.Features.Rooms.List;

/// <summary>
/// Arama teriminin makul uzunlukta olması. <c>page</c>/<c>pageSize</c> hatalı gelirse
/// <c>PageQuery</c> değeri sessizce sınırlara çeker (liste uçları 400 ile reddedilmez).
/// </summary>
public sealed class ListRoomsValidator : AbstractValidator<ListRoomsRequest>
{
    /// <summary>Oda numarası kolonunun sınırı; daha uzun arama anlamsızdır.</summary>
    private const int MaxSearchLength = 16;

    public ListRoomsValidator() =>
        RuleFor(request => request.Search).MaximumLength(MaxSearchLength);
}
