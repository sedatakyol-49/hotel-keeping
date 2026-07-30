using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Guests.Common;
using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.Guests.Create;

/// <summary>
/// Yeni misafir oluşturur. <c>HotelId</c> aktif otelden gelir.
/// <para>
/// Misafirde <b>benzersizlik kuralı yoktur</b>: aynı isim/e-posta ile birden çok kayıt meşrudur
/// (aynı adı taşıyan farklı kişiler, aile üyelerinin aynı e-postası). Tekilleştirme (merge)
/// ileride ayrı bir use-case olarak ele alınır; burada sessizce mevcut kayda bağlanmak
/// yanlış misafire konaklama yazma riski taşır.
/// </para>
/// </summary>
internal sealed class CreateGuestHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    GuestReader reader)
    : IRequestHandler<CreateGuestRequest, GuestResponse>
{
    public async Task<GuestResponse> Handle(CreateGuestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Konsolide modda hangi otele yazilacagi belirsizdir -> 400.
        var hotelId = currentUser.RequireHotelId();

        var guest = new Guest
        {
            HotelId = hotelId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = Normalize(request.Email),
            Phone = Normalize(request.Phone),
            Nationality = request.Nationality,
            AddressLine = Normalize(request.AddressLine),
            PostalCode = Normalize(request.PostalCode),
            City = Normalize(request.City),
            BirthDate = request.BirthDate,
            Culture = NormalizeCulture(request.Culture),
            Note = Normalize(request.Note),
        };

        database.Guests.Add(guest);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(guest.Id, cancellationToken).ConfigureAwait(false);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCulture(string? culture) =>
        string.IsNullOrWhiteSpace(culture) ? null : SupportedCultures.Normalize(culture);
}
