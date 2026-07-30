using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Guests.Common;

namespace HotelCore.Application.Features.Guests.Update;

internal sealed class UpdateGuestHandler(IAppDbContext database, GuestReader reader)
    : IRequestHandler<UpdateGuestRequest, GuestResponse>
{
    public async Task<GuestResponse> Handle(UpdateGuestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guest = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        guest.FirstName = request.FirstName.Trim();
        guest.LastName = request.LastName.Trim();
        guest.Email = Normalize(request.Email);
        guest.Phone = Normalize(request.Phone);
        guest.Nationality = request.Nationality;
        guest.AddressLine = Normalize(request.AddressLine);
        guest.PostalCode = Normalize(request.PostalCode);
        guest.City = Normalize(request.City);
        guest.BirthDate = request.BirthDate;
        guest.Culture = NormalizeCulture(request.Culture);
        guest.Note = Normalize(request.Note);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCulture(string? culture) =>
        string.IsNullOrWhiteSpace(culture) ? null : SupportedCultures.Normalize(culture);
}
