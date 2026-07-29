using FluentValidation;

namespace HotelCore.Application.Features.Auth.Refresh;

/// <summary>
/// Yalnızca biçimsel kontrol: token'ın geçerli/iptal olup olmadığı handler'da denetlenir
/// ve her durumda tek tip 401 döner.
/// </summary>
public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}
