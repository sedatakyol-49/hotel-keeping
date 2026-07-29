using FluentValidation;

namespace HotelCore.Application.Features.Auth.Login;

/// <summary>
/// Login girdisinin biçimsel doğrulaması. <b>Kimlik doğrulama sonucu burada değerlendirilmez</b> —
/// "kullanıcı yok" bilgisi 400 ile sızdırılmamalıdır (handler tek tip 401 döner).
/// </summary>
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}
