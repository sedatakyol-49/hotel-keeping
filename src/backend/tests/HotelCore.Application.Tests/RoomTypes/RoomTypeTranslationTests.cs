using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Features.RoomTypes.Common;
using HotelCore.Application.Features.RoomTypes.Create;
using HotelCore.Application.Features.RoomTypes.GetById;
using HotelCore.Application.Features.RoomTypes.List;
using HotelCore.Application.Features.RoomTypes.Update;
using HotelCore.Application.Features.Rooms.List;
using HotelCore.Application.Tests.Support;

namespace HotelCore.Application.Tests.RoomTypes;

/// <summary>
/// Dinamik icerik cevirilerinin cozumlenmesi (api-contracts.md → "Ceviri davranisi"):
/// yanitlar aktif dile gore cozumlenmis metni tasir; o dilde ceviri yoksa entity'deki
/// <b>varsayilan degere</b> dusulur.
/// <para>
/// Aktif dil <c>CultureInfo.CurrentUICulture</c>'dan okundugu icin (bkz. <c>RequestCulture</c>)
/// her test kulturu acikca ayarlar ve <b>geri koyar</b>; makinenin isletim sistemi diline
/// bagli kirilgan test kalmaz.
/// </para>
/// </summary>
public sealed class RoomTypeTranslationTests
{
    private const string DefaultName = "Doppelzimmer";

    private const string TurkishName = "Iki Kisilik Oda";

    private const string DefaultDescription = "Standart aciklama";

    private static CreateRoomTypeRequest RequestWithTranslations(
        string code,
        IReadOnlyDictionary<string, RoomTypeTranslationDto?>? translations) => new()
    {
        Code = code,
        Name = DefaultName,
        Description = DefaultDescription,
        BasePrice = 120m,
        Capacity = 2,
        Translations = translations
    };

    [Fact]
    public async Task Translation_of_the_requested_culture_is_returned_when_it_exists()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var created = await RoomModuleTestHost.WithCultureAsync("de", () =>
            host.Dispatcher.Send(RequestWithTranslations("TR1", new Dictionary<string, RoomTypeTranslationDto?>
            {
                ["tr"] = new() { Name = TurkishName, Description = "Turkce aciklama" }
            })));

        var detail = await RoomModuleTestHost.WithCultureAsync(
            "tr",
            () => host.Dispatcher.Send(new GetRoomTypeByIdRequest(created.Id)));

        detail.Name.Should().Be(TurkishName);
        detail.Description.Should().Be("Turkce aciklama");
    }

    [Fact]
    public async Task Missing_translation_falls_back_to_the_default_entity_value()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var created = await RoomModuleTestHost.WithCultureAsync("de", () =>
            host.Dispatcher.Send(RequestWithTranslations("TR2", new Dictionary<string, RoomTypeTranslationDto?>
            {
                ["tr"] = new() { Name = TurkishName }
            })));

        // "en" icin ceviri yok → entity'deki varsayilan metin doner.
        var detail = await RoomModuleTestHost.WithCultureAsync(
            "en",
            () => host.Dispatcher.Send(new GetRoomTypeByIdRequest(created.Id)));

        detail.Name.Should().Be(DefaultName);
        detail.Description.Should().Be(DefaultDescription);
    }

    [Fact]
    public async Task Partially_translated_record_falls_back_field_by_field()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var created = await RoomModuleTestHost.WithCultureAsync("de", () =>
            host.Dispatcher.Send(RequestWithTranslations("TR3", new Dictionary<string, RoomTypeTranslationDto?>
            {
                // Yalnizca ad cevrildi; aciklama gonderilmedi.
                ["tr"] = new() { Name = TurkishName }
            })));

        var detail = await RoomModuleTestHost.WithCultureAsync(
            "tr",
            () => host.Dispatcher.Send(new GetRoomTypeByIdRequest(created.Id)));

        detail.Name.Should().Be(TurkishName);
        detail.Description.Should().Be(DefaultDescription, "eksik alan alan bazinda fallback yapar");
    }

    [Fact]
    public async Task Unsupported_culture_falls_back_to_the_default_language()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var created = await RoomModuleTestHost.WithCultureAsync("de", () =>
            host.Dispatcher.Send(RequestWithTranslations("TR4", new Dictionary<string, RoomTypeTranslationDto?>
            {
                ["de"] = new() { Name = "Doppelzimmer DE" },
                ["tr"] = new() { Name = TurkishName }
            })));

        // "fr" desteklenmiyor → RequestCulture varsayilan dile ("de") duser.
        var detail = await RoomModuleTestHost.WithCultureAsync(
            "fr",
            () => host.Dispatcher.Send(new GetRoomTypeByIdRequest(created.Id)));

        detail.Name.Should().Be("Doppelzimmer DE");
    }

    [Fact]
    public async Task Detail_response_returns_every_culture_for_the_edit_screen()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var created = await RoomModuleTestHost.WithCultureAsync("de", () =>
            host.Dispatcher.Send(RequestWithTranslations("TR5", new Dictionary<string, RoomTypeTranslationDto?>
            {
                ["de"] = new() { Name = "Doppelzimmer DE" },
                ["en"] = new() { Name = "Double Room" },
                ["tr"] = new() { Name = TurkishName }
            })));

        var detail = await RoomModuleTestHost.WithCultureAsync(
            "de",
            () => host.Dispatcher.Send(new GetRoomTypeByIdRequest(created.Id)));

        detail.Translations.Should().NotBeNull();
        detail.Translations!.Keys.Should().BeEquivalentTo("de", "en", "tr");
        detail.Translations["en"].Name.Should().Be("Double Room");
    }

    [Fact]
    public async Task Listing_omits_the_translations_dictionary()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await RoomModuleTestHost.WithCultureAsync("de", () =>
            host.Dispatcher.Send(RequestWithTranslations("TR6", new Dictionary<string, RoomTypeTranslationDto?>
            {
                ["tr"] = new() { Name = TurkishName }
            })));

        var listed = await RoomModuleTestHost.WithCultureAsync(
            "tr",
            () => host.Dispatcher.Send(new ListRoomTypesRequest()));

        var item = listed.Should().ContainSingle(entry => entry.Code == "TR6").Subject;
        item.Name.Should().Be(TurkishName, "liste de aktif dile gore cozumlenir");
        item.Translations.Should().BeNull("sozlesme: liste yanitinda translations DONMEZ");
    }

    [Fact]
    public async Task Sending_a_null_culture_value_deletes_that_translation()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var created = await RoomModuleTestHost.WithCultureAsync("de", () =>
            host.Dispatcher.Send(RequestWithTranslations("TR7", new Dictionary<string, RoomTypeTranslationDto?>
            {
                ["tr"] = new() { Name = TurkishName }
            })));

        var updated = await RoomModuleTestHost.WithCultureAsync("tr", () =>
            host.Dispatcher.Send(new UpdateRoomTypeRequest
            {
                Id = created.Id,
                Code = "TR7",
                Name = DefaultName,
                Description = DefaultDescription,
                BasePrice = 120m,
                Capacity = 2,
                Translations = new Dictionary<string, RoomTypeTranslationDto?> { ["tr"] = null }
            }));

        updated.Translations.Should().NotContainKey("tr");
        updated.Name.Should().Be(DefaultName, "ceviri silindikten sonra varsayilan metne dusulur");
    }

    [Fact]
    public async Task Room_listing_resolves_the_room_type_name_for_the_active_culture()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddTranslationAsync(host.RoomTypeId, "tr", TranslationFields.Name, TurkishName);
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "601");

        var page = await RoomModuleTestHost.WithCultureAsync(
            "tr",
            () => host.Dispatcher.Send(new ListRoomsRequest()));

        page.Items.Should().ContainSingle().Which.RoomTypeName.Should().Be(TurkishName);
    }

    [Fact]
    public async Task Room_listing_falls_back_to_the_default_room_type_name()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddTranslationAsync(host.RoomTypeId, "tr", TranslationFields.Name, TurkishName);
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "602");

        // "en" icin ceviri yok → oda tipi adi entity'deki varsayilan degerdir.
        var page = await RoomModuleTestHost.WithCultureAsync(
            "en",
            () => host.Dispatcher.Send(new ListRoomsRequest()));

        page.Items.Should().ContainSingle().Which.RoomTypeName.Should().Be(DefaultName);
    }

    [Fact]
    public async Task Unsupported_translation_culture_is_rejected_by_validation()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var act = async () => await RoomModuleTestHost.WithCultureAsync("de", () =>
            host.Dispatcher.Send(RequestWithTranslations("TR8", new Dictionary<string, RoomTypeTranslationDto?>
            {
                ["fr"] = new() { Name = "Chambre double" }
            })));

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey(nameof(CreateRoomTypeRequest.Translations));
    }
}
