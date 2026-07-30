using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hotels.Common;

namespace HotelCore.Application.Features.Hotels.UpdateSettings;

/// <summary>
/// Otel künyesini ve vergi profilini günceller. Erişilemeyen otel 404 döner
/// (bkz. <see cref="HotelReader"/>).
/// </summary>
internal sealed class UpdateHotelSettingsHandler(
    IAppDbContext database,
    HotelReader reader)
    : IRequestHandler<UpdateHotelSettingsRequest, HotelResponse>
{
    public async Task<HotelResponse> Handle(
        UpdateHotelSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hotel = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        hotel.Name = request.Name.Trim();
        hotel.Country = request.Country;
        hotel.City = request.City.Trim();
        hotel.AddressLine = Normalize(request.AddressLine);
        hotel.PostalCode = Normalize(request.PostalCode);
        hotel.Phone = Normalize(request.Phone);
        hotel.Email = Normalize(request.Email);
        hotel.TaxNumber = Normalize(request.TaxNumber);
        hotel.DefaultCulture = SupportedCultures.Normalize(request.DefaultCulture);
        hotel.Currency = request.Currency.Trim().ToUpperInvariant();

        hotel.TaxProfile.VatRate = request.TaxProfile.VatRate;
        hotel.TaxProfile.ReducedVatRate = request.TaxProfile.ReducedVatRate;
        hotel.TaxProfile.CityTaxPerPersonNight = request.TaxProfile.CityTaxPerPersonNight;
        hotel.TaxProfile.CityTaxEnabled = request.TaxProfile.CityTaxEnabled;

        // Kurtaxe cocuk muafiyeti. Yas siniri muafiyet KAPALIYKEN de saklanir: otelin belediye
        // kurali (orn. "18 yas alti") muafiyet gecici olarak kapatildiginda kaybolmasin.
        hotel.TaxProfile.CityTaxExemptChildren = request.TaxProfile.CityTaxExemptChildren;
        hotel.TaxProfile.CityTaxChildAgeLimit = request.TaxProfile.CityTaxChildAgeLimit;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Bos/bosluk metni <c>null</c>'a indirger — "" ile null ayrimi veride tutulmaz.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
