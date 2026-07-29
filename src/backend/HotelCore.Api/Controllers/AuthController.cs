using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Auth.Common;
using HotelCore.Application.Features.Auth.GetCurrentUser;
using HotelCore.Application.Features.Auth.Login;
using HotelCore.Application.Features.Auth.Refresh;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>Kimlik doğrulama uç noktaları (api-contracts.md → Auth).</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>E-posta + parola ile oturum açar.</summary>
    /// <remarks>
    /// Başarısız girişlerde <b>her zaman</b> aynı 401 yanıtı döner; kullanıcının var olup
    /// olmadığı bilgisi sızdırılmaz.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<LoginResponse> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // IP gövdeden değil bağlantıdan alınır (denetim izi için).
        return dispatcher.Send(request with { IpAddress = ClientIpAddress() }, cancellationToken);
    }

    /// <summary>Rotating refresh: kullanılan token iptal edilir, yeni token çifti üretilir.</summary>
    /// <remarks>Yanıt yalnızca token bilgisidir; kullanıcı nesnesi içermez.</remarks>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthTokensDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthTokensDto> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return dispatcher.Send(request with { IpAddress = ClientIpAddress() }, cancellationToken);
    }

    /// <summary>Aktif kullanıcının profili: roller, izinler ve erişilebilir oteller.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<UserProfileDto> Me(CancellationToken cancellationToken) =>
        dispatcher.Send(new GetCurrentUserRequest(), cancellationToken);

    /// <summary>Ters vekil arkasında da anlamlı olacak şekilde istemci IP'sini döndürür.</summary>
    private string? ClientIpAddress()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        // IPv4-mapped IPv6 adresleri (::ffff:127.0.0.1) okunabilir biçime indirgenir.
        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
    }
}
