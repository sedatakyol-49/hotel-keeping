using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Auth.Login;

/// <summary>
/// <c>POST /api/v1/auth/login</c> gövdesi.
/// </summary>
/// <param name="Email">Kullanıcı e-postası (büyük/küçük harf duyarsız).</param>
/// <param name="Password">Düz metin parola — asla loglanmaz/saklanmaz.</param>
public sealed record LoginRequest(string Email, string Password) : IRequest<LoginResponse>
{
    /// <summary>
    /// İstemci IP'si. Refresh token'ın kaynağını denetim izinde tutmak için kullanılır;
    /// controller tarafından doldurulur, istek gövdesinden OKUNMAZ.
    /// </summary>
    [JsonIgnore]
    public string? IpAddress { get; init; }
}
