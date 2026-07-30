using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Hotels.GetById;
using HotelCore.Application.Features.Hotels.List;
using HotelCore.Application.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Tests.Settings;

/// <summary>
/// <c>HotelReader</c> erisim kapsami testleri (api-contracts.md → "Hotels &amp; Ayarlar").
/// <para>
/// <b>Neden ayri bir kural:</b> <c>Hotel</c> tenant-scoped bir entity DEGILDIR (tenant kokunun
/// kendisidir), bu yuzden <c>AppDbContext</c>'teki global query filter onu suzmez. Erisim
/// <c>UserHotelAccess</c> tablosundan — yani JWT claim'i degil <b>veritabani</b> esas alinarak —
/// dogrulanir; boylece erisim iptali token'in suresinin bitmesini beklemez.
/// </para>
/// <para>
/// Sahne iki marka icerir (bkz. <see cref="SettingsAndPersonnelTestHost"/>): tek markali bir
/// sahnede "baska markanin oteli gorunmez" iddiasi kendiliginden yesil gorunurdu.
/// </para>
/// </summary>
public sealed class HotelAccessScopeTests
{
    [Fact]
    public async Task All_hotels_permission_is_still_limited_to_the_users_own_head_office()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Head Office kullanicisi: konsolide baglam (aktif otel yok) + allHotels bypass.
        host.CurrentUser.CanAccessAllHotels = true;
        host.CurrentUser.HotelId = null;
        host.CurrentUser.HeadOfficeId = host.HeadOfficeId;

        var hotels = await host.Dispatcher.Send(new ListHotelsRequest());

        hotels.Select(hotel => hotel.Id).Should().BeEquivalentTo([host.HotelId, host.OtherHotelId]);
        hotels.Select(hotel => hotel.Id).Should().NotContain(
            host.OtherBrandHotelId,
            "allHotels yetkisi marka sinirini asmaz");
    }

    [Fact]
    public async Task Hotel_of_another_brand_is_reported_as_not_found_for_a_head_office_user()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        host.CurrentUser.CanAccessAllHotels = true;
        host.CurrentUser.HotelId = null;

        var act = async () =>
            await host.Dispatcher.Send(new GetHotelByIdRequest(host.OtherBrandHotelId));

        // 403 DEGIL 404: otelin var oldugu bilgisi bile sizdirilmaz.
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task All_hotels_permission_without_a_head_office_claim_grants_nothing()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Bozuk/eksik token: allHotels var ama hangi markaya ait oldugu belli degil.
        // "Guvenli varsayilan" kapalidir: hicbir otel gorunmez.
        host.CurrentUser.CanAccessAllHotels = true;
        host.CurrentUser.HeadOfficeId = null;
        host.CurrentUser.HotelId = null;

        var hotels = await host.Dispatcher.Send(new ListHotelsRequest());

        hotels.Should().BeEmpty();
    }

    [Fact]
    public async Task User_without_all_hotels_sees_only_explicitly_granted_hotels()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId, isDefault: true);

        var hotels = await host.Dispatcher.Send(new ListHotelsRequest());

        hotels.Select(hotel => hotel.Id).Should().Equal(host.HotelId);
    }

    [Fact]
    public async Task Regional_manager_sees_every_hotel_granted_in_the_access_table()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId, isDefault: true);
        await host.GrantHotelAccessAsync(host.OtherHotelId);

        var hotels = await host.Dispatcher.Send(new ListHotelsRequest());

        hotels.Select(hotel => hotel.Id).Should().BeEquivalentTo([host.HotelId, host.OtherHotelId]);
    }

    [Fact]
    public async Task Hotel_without_an_access_row_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var act = async () => await host.Dispatcher.Send(new GetHotelByIdRequest(host.OtherHotelId));

        await act.Should().ThrowAsync<NotFoundException>(
            "erisilemeyen otel 404 doner; 403 otelin varligini sizdirirdi");
    }

    [Fact]
    public async Task Revoking_the_access_row_hides_the_hotel_even_though_the_token_is_unchanged()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var beforeRevocation = await host.Dispatcher.Send(new ListHotelsRequest());

        // Erisim satiri silinir; kimlik (token) aynen kalir.
        var access = await host.Database.UserHotelAccesses
            .FirstAsync(row => row.UserId == host.UserId && row.HotelId == host.HotelId);
        host.Database.UserHotelAccesses.Remove(access);
        await host.Database.SaveChangesAsync();
        host.Database.ChangeTracker.Clear();

        var afterRevocation = await host.Dispatcher.Send(new ListHotelsRequest());

        beforeRevocation.Should().HaveCount(1);
        afterRevocation.Should().BeEmpty("erisim veritabanindan okunur, token'in suresi beklenmez");
    }

    [Fact]
    public async Task Anonymous_identity_sees_no_hotels()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        // Kimlikte kullanici yok (ornegin arka plan islemi): hicbir otel donmemelidir.
        host.CurrentUser.UserId = null;

        var hotels = await host.Dispatcher.Send(new ListHotelsRequest());

        hotels.Should().BeEmpty();
    }

    [Fact]
    public async Task Hotel_list_counts_only_live_rooms()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "101");
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "102");
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "103", isDeleted: true);

        var hotels = await host.Dispatcher.Send(new ListHotelsRequest());

        hotels.Should().ContainSingle().Which.RoomCount.Should().Be(
            2,
            "kapatilan (soft-delete edilmis) oda sayilmaz");
    }

    [Fact]
    public async Task Hotel_detail_returns_the_tax_profile_and_the_country_as_an_enum_name()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.GrantHotelAccessAsync(host.HotelId);

        var hotel = await host.Dispatcher.Send(new GetHotelByIdRequest(host.HotelId));

        hotel.HeadOfficeId.Should().Be(host.HeadOfficeId);
        hotel.Country.Should().Be("DE", "ulke enum ADI olarak doner, sayi degil");
        hotel.TaxProfile.VatRate.Should().Be(19m);
        hotel.TaxProfile.ReducedVatRate.Should().Be(7m);
    }
}
