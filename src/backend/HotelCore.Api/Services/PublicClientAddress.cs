using System.Globalization;
using System.Net;
using System.Net.Sockets;
using HotelCore.Api.Startup;
using Microsoft.Extensions.Options;

namespace HotelCore.Api.Services;

/// <summary>
/// İstemci adresinin <b>tek</b> çözüm yeri (api-contracts-public-booking.md §1.2).
///
/// <para><b>IPv6 <c>/64</c> ile daraltılır:</b> bir aboneye tipik olarak bütün bir <c>/64</c>
/// prefix'i verilir; adres bazında sınırlamak, aynı kullanıcının her istekte yeni bir adres
/// üretip sınırı sonsuza kadar sıfırlamasına izin verirdi. IPv4 <c>/32</c> (adresin kendisi)
/// kullanılır.</para>
///
/// <para><b><c>X-Forwarded-For</c> yalnızca güvenilen proxy'den okunur.</b> Koşulsuz okumak, hız
/// sınırını istemcinin <i>kendi beyanına</i> bağlardı — header uydurmak bedava olduğu için sınır
/// tamamen anlamsızlaşırdı.</para>
/// </summary>
public sealed class PublicClientAddress(IOptions<PublicChannelSettings> settings)
{
    private readonly HashSet<IPAddress> _trustedProxies =
        settings.Value.TrustedProxies
            .Select(value => IPAddress.TryParse(value, out var address) ? address : null)
            .Where(address => address is not null)
            .Select(address => address!)
            .ToHashSet();

    /// <summary>Hız sınırı ve IP özeti için normalize edilmiş istemci adresi; bilinmiyorsa <c>null</c>.</summary>
    public string? Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var remote = context.Connection.RemoteIpAddress;
        var candidate = remote;

        if (remote is not null && _trustedProxies.Contains(remote))
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                // Zincirin İLK girdisi asıl istemcidir; sonrakiler proxy'lerdir.
                var first = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
                if (IPAddress.TryParse(first, out var parsed))
                {
                    candidate = parsed;
                }
            }
        }

        return candidate is null ? null : Normalize(candidate);
    }

    /// <summary>IPv6'yı <c>/64</c> prefix'ine indirger; IPv4 olduğu gibi kalır.</summary>
    private static string Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily is not AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{new IPAddress(bytes)}/64");
    }
}
