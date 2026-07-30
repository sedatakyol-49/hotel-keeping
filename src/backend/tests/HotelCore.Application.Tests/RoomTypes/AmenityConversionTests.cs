using AwesomeAssertions;
using HotelCore.Application.Features.RoomTypes.Create;
using HotelCore.Application.Features.RoomTypes.GetById;
using HotelCore.Application.Features.RoomTypes.List;
using HotelCore.Application.Features.RoomTypes.Update;
using HotelCore.Application.Tests.Support;

namespace HotelCore.Application.Tests.RoomTypes;

/// <summary>
/// <c>amenities</c> alaninin cift yonlu donusumu (api-contracts.md → "Sekiller"):
/// veritabaninda <b>virgulle ayrilmis tek metin</b>, API'de <b>dizi</b>.
/// <para>
/// Testler donusumu ic siniftan (<c>AmenityList</c>) degil <b>gozlemlenebilir davranistan</b>
/// dogrular: istegin ucundan kolona, kolondan yanit dizisine.
/// </para>
/// </summary>
public sealed class AmenityConversionTests
{
    private static CreateRoomTypeRequest Request(string code, IReadOnlyList<string>? amenities) => new()
    {
        Code = code,
        Name = "Testzimmer",
        BasePrice = 100m,
        Capacity = 2,
        Amenities = amenities
    };

    [Fact]
    public async Task Amenity_array_is_stored_as_a_single_comma_separated_column()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request("AM1", ["wifi", "minibar", "balcony"]));

        created.Amenities.Should().Equal("wifi", "minibar", "balcony");
        (await host.FindRoomTypeIncludingDeletedAsync(created.Id))!.Amenities
            .Should().Be("wifi,minibar,balcony");
    }

    [Fact]
    public async Task Comma_separated_column_is_returned_as_an_array_with_trimmed_items()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        // Kolonu dogrudan (API'den gecmeden) yaz: okuma yonunu izole eder.
        var roomType = await host.AddRoomTypeAsync(
            host.HotelId,
            "AM2",
            "Testzimmer",
            amenities: "wifi, minibar ,, balcony,");

        var detail = await host.Dispatcher.Send(new GetRoomTypeByIdRequest(roomType.Id));

        detail.Amenities.Should().Equal("wifi", "minibar", "balcony");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_amenity_items_are_dropped(string blankItem)
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request("AM3", ["wifi", blankItem, "safe"]));

        created.Amenities.Should().Equal("wifi", "safe");
    }

    [Fact]
    public async Task Amenity_items_are_trimmed_before_they_are_stored()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request("AM4", ["  wifi  ", "\tsafe\t"]));

        (await host.FindRoomTypeIncludingDeletedAsync(created.Id))!.Amenities.Should().Be("wifi,safe");
    }

    [Fact]
    public async Task Duplicate_amenity_keys_are_removed_case_insensitively()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request("AM5", ["wifi", "WiFi", "WIFI", "safe"]));

        created.Amenities.Should().Equal("wifi", "safe");
    }

    [Fact]
    public async Task An_all_blank_amenity_list_leaves_the_column_null_and_returns_an_empty_array()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request("AM6", ["", "  ", "\t"]));

        created.Amenities.Should().BeEmpty();
        (await host.FindRoomTypeIncludingDeletedAsync(created.Id))!.Amenities
            .Should().BeNull("bos liste kolonu bos metinle degil null ile temsil eder");
    }

    [Fact]
    public async Task Omitted_amenities_are_returned_as_an_empty_array_never_null()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request("AM7", null));

        created.Amenities.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Update_replaces_the_whole_amenity_list()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var created = await host.Dispatcher.Send(Request("AM8", ["wifi", "minibar"]));

        var updated = await host.Dispatcher.Send(new UpdateRoomTypeRequest
        {
            Id = created.Id,
            Code = "AM8",
            Name = "Testzimmer",
            BasePrice = 100m,
            Capacity = 2,
            Amenities = ["balcony"]
        });

        updated.Amenities.Should().Equal("balcony");
        (await host.FindRoomTypeIncludingDeletedAsync(created.Id))!.Amenities.Should().Be("balcony");
    }

    [Fact]
    public async Task Update_without_amenities_clears_the_column()
    {
        // PUT tam guncellemedir: gonderilmeyen alan "degismeden kalsin" anlamina GELMEZ.
        await using var host = await RoomModuleTestHost.CreateAsync();
        var created = await host.Dispatcher.Send(Request("AM9", ["wifi"]));

        var updated = await host.Dispatcher.Send(new UpdateRoomTypeRequest
        {
            Id = created.Id,
            Code = "AM9",
            Name = "Testzimmer",
            BasePrice = 100m,
            Capacity = 2
        });

        updated.Amenities.Should().BeEmpty();
        (await host.FindRoomTypeIncludingDeletedAsync(created.Id))!.Amenities.Should().BeNull();
    }

    [Fact]
    public async Task Amenities_survive_the_round_trip_through_the_listing_endpoint()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.Dispatcher.Send(Request("AMA", ["wifi", "safe"]));

        var listed = await host.Dispatcher.Send(new ListRoomTypesRequest());

        listed.Should().ContainSingle(item => item.Code == "AMA")
            .Which.Amenities.Should().Equal("wifi", "safe");
    }
}
