using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Application.Features.Hotels.UpdateSettings;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Settings;

/// <summary>
/// <c>PUT /hotels/{id}/settings</c> handler testleri (api-contracts.md → "Hotels &amp; Ayarlar").
/// <para>
/// Odak: <b>normalizasyon</b> (para birimi buyuk harf, kultur kodu indirgeme, kirpma, bos metin →
/// <c>null</c>), vergi profilinin kalici olmasi ve erisim kuralinin yazma yolunda da gecerli olmasi.
/// Testler yaniti degil <b>veritabanindaki satiri</b> de dogrular: yalnizca yanita bakmak,
/// normalizasyonun projeksiyonda yapilmis olma ihtimalini elemez.
/// </para>
/// </summary>
public sealed class UpdateHotelSettingsHandlerTests
{
    [Fact]
    public async Task Currency_is_normalised_to_upper_case()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var response = await host.Dispatcher.Send(Request(host.HotelId) with { Currency = "chf" });

        response.Currency.Should().Be("CHF");
        (await host.FindHotelAsync(host.HotelId))!.Currency.Should().Be(
            "CHF",
            "ISO 4217 kodu buyuk harfle SAKLANIR; kucuk harf gonderilse bile");
    }

    [Fact]
    public async Task Currency_padded_with_whitespace_is_rejected_by_validation()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        // Sozlesme "tam 3 harf" der: bosluklu deger dogrulamada elenir (handler'daki Trim
        // yalnizca ikinci bir savunma katmanidir). Istemci ham kullanici girdisini
        // temizlemeden gonderirse 400 alir.
        var act = async () => await host.Dispatcher.Send(
            Request(host.HotelId) with { Currency = " try " });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData("DE", "de")]
    [InlineData("EN", "en")]
    [InlineData("tr-TR", "tr")]
    [InlineData(" de-DE ", "de")]
    public async Task Default_culture_is_normalised_to_a_two_letter_lower_case_code(
        string sent,
        string expected)
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var response = await host.Dispatcher.Send(
            Request(host.HotelId) with { DefaultCulture = sent });

        response.DefaultCulture.Should().Be(expected);
        (await host.FindHotelAsync(host.HotelId))!.DefaultCulture.Should().Be(expected);
    }

    [Fact]
    public async Task Blank_optional_fields_are_stored_as_null_instead_of_an_empty_string()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        // Once dolu degerler yazilir; sonra ayni alanlar bosluk/bos metinle temizlenir.
        await host.Dispatcher.Send(Request(host.HotelId) with
        {
            AddressLine = "Hauptstrasse 1",
            PostalCode = "10115",
            Phone = "+49 30 123",
            Email = "info@hotel.test",
            TaxNumber = "DE123456789"
        });

        var response = await host.Dispatcher.Send(Request(host.HotelId) with
        {
            AddressLine = "   ",
            PostalCode = string.Empty,
            Phone = "\t",
            Email = string.Empty,
            TaxNumber = "  "
        });

        response.AddressLine.Should().BeNull();
        response.PostalCode.Should().BeNull();
        response.Phone.Should().BeNull();
        response.Email.Should().BeNull();
        response.TaxNumber.Should().BeNull();

        var stored = (await host.FindHotelAsync(host.HotelId))!;
        stored.AddressLine.Should().BeNull("\"\" ile null ayrimi veride tutulmaz");
        stored.PostalCode.Should().BeNull();
        stored.Phone.Should().BeNull();
        stored.Email.Should().BeNull();
        stored.TaxNumber.Should().BeNull();
    }

    [Fact]
    public async Task Optional_fields_are_trimmed_when_they_carry_a_value()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var response = await host.Dispatcher.Send(Request(host.HotelId) with
        {
            Name = "  Hotel Alpha  ",
            City = "  Muenchen  ",
            AddressLine = "  Hauptstrasse 1  ",
            TaxNumber = " DE123456789 "
        });

        response.Name.Should().Be("Hotel Alpha");
        response.City.Should().Be("Muenchen");
        response.AddressLine.Should().Be("Hauptstrasse 1");
        response.TaxNumber.Should().Be("DE123456789");
    }

    [Fact]
    public async Task Tax_profile_is_persisted_and_returned()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var response = await host.Dispatcher.Send(Request(host.HotelId) with
        {
            TaxProfile = new TaxProfileDto
            {
                VatRate = 20m,
                ReducedVatRate = 10m,
                CityTaxPerPersonNight = 3.50m,
                CityTaxEnabled = true
            }
        });

        response.TaxProfile.VatRate.Should().Be(20m);
        response.TaxProfile.ReducedVatRate.Should().Be(10m);
        response.TaxProfile.CityTaxPerPersonNight.Should().Be(3.50m);
        response.TaxProfile.CityTaxEnabled.Should().BeTrue();

        // Oranlar koda hardcode edilmez (architecture.md §4.1): otel satirinda saklanir.
        var stored = (await host.FindHotelAsync(host.HotelId))!.TaxProfile;
        stored.VatRate.Should().Be(20m);
        stored.CityTaxPerPersonNight.Should().Be(3.50m);
        stored.CityTaxEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Country_is_stored_and_returned_as_an_enum_name()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var response = await host.Dispatcher.Send(
            Request(host.HotelId) with { Country = Country.AT, City = "Wien" });

        response.Country.Should().Be("AT");
        (await host.FindHotelAsync(host.HotelId))!.Country.Should().Be(Country.AT);
    }

    [Fact]
    public async Task Updating_a_hotel_the_user_cannot_access_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var act = async () => await host.Dispatcher.Send(
            Request(host.OtherHotelId) with { Name = "Uebernommen" });

        await act.Should().ThrowAsync<NotFoundException>();
        (await host.FindHotelAsync(host.OtherHotelId))!.Name.Should().Be(
            "Hotel B",
            "reddedilen istek veriyi degistirmemelidir");
    }

    [Fact]
    public async Task Updating_a_hotel_of_another_brand_is_reported_as_not_found_for_a_head_office_user()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        host.CurrentUser.CanAccessAllHotels = true;
        host.CurrentUser.HotelId = null;

        var act = async () => await host.Dispatcher.Send(
            Request(host.OtherBrandHotelId) with { Name = "Uebernommen" });

        await act.Should().ThrowAsync<NotFoundException>();
        (await host.FindHotelAsync(host.OtherBrandHotelId))!.Name.Should().Be("Fremdmarke Hotel");
    }

    [Fact]
    public async Task Unknown_hotel_id_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var act = async () => await host.Dispatcher.Send(Request(Guid.NewGuid()));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("EURO")]
    [InlineData("EU")]
    [InlineData("12")]
    [InlineData("")]
    public async Task Currency_that_is_not_a_three_letter_code_is_rejected(string currency)
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var act = async () => await host.Dispatcher.Send(
            Request(host.HotelId) with { Currency = currency });

        // Dogrulama boru hatti (ValidationBehavior) handler'a hic ulasmadan reddeder.
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Unsupported_default_culture_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var act = async () => await host.Dispatcher.Send(
            Request(host.HotelId) with { DefaultCulture = "fr" });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey(nameof(UpdateHotelSettingsRequest.DefaultCulture));
    }

    [Fact]
    public async Task Invalid_email_is_rejected_but_a_blank_email_is_accepted()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var invalid = async () => await host.Dispatcher.Send(
            Request(host.HotelId) with { Email = "not-an-email" });

        await invalid.Should().ThrowAsync<ValidationException>();

        // Bos e-posta "verilmedi" anlamindadir; e-posta bicim kurali uygulanmaz.
        var response = await host.Dispatcher.Send(Request(host.HotelId) with { Email = "   " });
        response.Email.Should().BeNull();
    }

    [Fact]
    public async Task Vat_rate_above_one_hundred_percent_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var act = async () => await host.Dispatcher.Send(Request(host.HotelId) with
        {
            TaxProfile = new TaxProfileDto { VatRate = 101m, ReducedVatRate = 7m }
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    /// <summary>Gecerli bir temel istek; testler yalnizca ilgilendikleri alani degistirir.</summary>
    private static UpdateHotelSettingsRequest Request(Guid hotelId) => new()
    {
        Id = hotelId,
        Name = "Hotel A",
        Country = Country.DE,
        City = "Berlin",
        DefaultCulture = "de",
        Currency = "EUR",
        TaxProfile = new TaxProfileDto { VatRate = 19m, ReducedVatRate = 7m }
    };
}
