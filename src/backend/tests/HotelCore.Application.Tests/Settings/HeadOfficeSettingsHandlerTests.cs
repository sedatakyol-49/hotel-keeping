using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.HeadOffices.GetSettings;
using HotelCore.Application.Features.HeadOffices.UpdateSettings;
using HotelCore.Application.Tests.Support;

namespace HotelCore.Application.Tests.Settings;

/// <summary>
/// <c>GET/PUT /head-office/settings</c> handler testleri.
/// <para>
/// Sozlesme: hangi Head Office okunacagi/yazilacagi <b>kimlikten</b> gelir (JWT
/// <c>headOfficeId</c> claim'i); istekte kimlik tasinmaz. Bu yuzden marka sinirini asan bir
/// istek "yetkisiz" degil <b>imkansiz</b> olmalidir — testler bunu iki markali sahnede dogrular.
/// </para>
/// </summary>
public sealed class HeadOfficeSettingsHandlerTests
{
    [Fact]
    public async Task Reading_settings_without_a_head_office_claim_is_forbidden()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Kimlikte headOfficeId yok: bu bir dogrulama hatasi degil, yetki baglami eksikligidir.
        host.CurrentUser.HeadOfficeId = null;

        var act = async () => await host.Dispatcher.Send(new GetHeadOfficeSettingsRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Updating_settings_without_a_head_office_claim_is_forbidden_and_changes_nothing()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        host.CurrentUser.HeadOfficeId = null;

        var act = async () => await host.Dispatcher.Send(new UpdateHeadOfficeSettingsRequest
        {
            BrandName = "Kaper Gruppe",
            DefaultCulture = "en"
        });

        await act.Should().ThrowAsync<ForbiddenException>();
        (await host.FindHeadOfficeAsync(host.HeadOfficeId))!.BrandName.Should().Be("Marka A Gruppe");
    }

    [Fact]
    public async Task Settings_are_read_from_the_head_office_in_the_identity()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var settings = await host.Dispatcher.Send(new GetHeadOfficeSettingsRequest());

        settings.Id.Should().Be(host.HeadOfficeId);
        settings.BrandName.Should().Be("Marka A Gruppe");
        settings.Id.Should().NotBe(host.OtherBrandHeadOfficeId);
    }

    [Fact]
    public async Task Hotel_count_covers_only_the_live_hotels_of_the_own_brand()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Sahne: markanin iki canli oteli var; ucuncusu kapatilmis, dorduncusu baska markada.
        await host.AddHotelAsync(host.HeadOfficeId, "Hotel C", isDeleted: true);
        await host.AddHotelAsync(host.OtherBrandHeadOfficeId, "Fremdmarke Hotel 2");

        var settings = await host.Dispatcher.Send(new GetHeadOfficeSettingsRequest());

        settings.HotelCount.Should().Be(2, "kapatilan otel ve baska markanin oteli sayilmaz");
    }

    [Fact]
    public async Task Brand_name_is_trimmed_and_default_culture_is_normalised()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var response = await host.Dispatcher.Send(new UpdateHeadOfficeSettingsRequest
        {
            BrandName = "  Marka A Hotels  ",
            DefaultCulture = "TR-tr"
        });

        response.BrandName.Should().Be("Marka A Hotels");
        response.DefaultCulture.Should().Be("tr");

        var stored = (await host.FindHeadOfficeAsync(host.HeadOfficeId))!;
        stored.BrandName.Should().Be("Marka A Hotels");
        stored.DefaultCulture.Should().Be("tr");
    }

    [Fact]
    public async Task Updating_the_own_brand_never_touches_another_brand()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        await host.Dispatcher.Send(new UpdateHeadOfficeSettingsRequest
        {
            BrandName = "Marka A Neu",
            DefaultCulture = "en"
        });

        (await host.FindHeadOfficeAsync(host.OtherBrandHeadOfficeId))!.BrandName.Should().Be(
            "Marka B Gruppe",
            "istek govdesinde Head Office kimligi tasinmaz; baska marka etkilenemez");
    }

    [Fact]
    public async Task A_head_office_claim_pointing_to_another_brand_reads_only_that_brand()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Kullanici marka B'ye baglaniyorsa marka B'nin ayarlarini gorur — marka A'nin degil.
        host.CurrentUser.HeadOfficeId = host.OtherBrandHeadOfficeId;

        var settings = await host.Dispatcher.Send(new GetHeadOfficeSettingsRequest());

        settings.Id.Should().Be(host.OtherBrandHeadOfficeId);
        settings.BrandName.Should().Be("Marka B Gruppe");
    }

    [Fact]
    public async Task A_head_office_claim_that_no_longer_exists_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        host.CurrentUser.HeadOfficeId = Guid.NewGuid();

        var act = async () => await host.Dispatcher.Send(new GetHeadOfficeSettingsRequest());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Empty_brand_name_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new UpdateHeadOfficeSettingsRequest
        {
            BrandName = "   ",
            DefaultCulture = "de"
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Unsupported_default_culture_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new UpdateHeadOfficeSettingsRequest
        {
            BrandName = "Marka A Gruppe",
            DefaultCulture = "es"
        });

        await act.Should().ThrowAsync<ValidationException>();
    }
}
