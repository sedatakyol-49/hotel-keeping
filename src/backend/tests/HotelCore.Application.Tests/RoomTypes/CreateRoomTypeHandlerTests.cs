using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.RoomTypes.Create;
using HotelCore.Application.Features.RoomTypes.Delete;
using HotelCore.Application.Features.RoomTypes.GetById;
using HotelCore.Application.Tests.Support;

namespace HotelCore.Application.Tests.RoomTypes;

/// <summary>
/// <c>POST /room-types</c> handler testleri: kod benzersizligi (409), otel kapsami ve
/// yanittaki turetilmis alanlar (<c>currency</c>, <c>roomCount</c>).
/// </summary>
public sealed class CreateRoomTypeHandlerTests
{
    private static CreateRoomTypeRequest Request(string code, string name = "Testzimmer") => new()
    {
        Code = code,
        Name = name,
        BasePrice = 99.90m,
        Capacity = 2
    };

    [Fact]
    public async Task Duplicate_code_in_the_same_hotel_is_rejected_as_a_conflict()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        // "DBL" host kurulumunda A otelinde zaten var.
        var act = async () => await host.Dispatcher.Send(Request("DBL"));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Code_uniqueness_ignores_surrounding_whitespace()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(Request("  DBL  "));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Same_code_in_a_different_hotel_is_allowed()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        // A otelinde "DBL" var, B otelinde yok. Aktif otel B iken ayni kod kullanilabilir:
        // benzersizlik kapsami (HotelId, Code) ciftidir, global degildir.
        host.CurrentUser.HotelId = host.OtherHotelId;

        var created = await host.Dispatcher.Send(Request("DBL"));

        created.Code.Should().Be("DBL");
        (await host.FindRoomTypeIncludingDeletedAsync(created.Id))!.HotelId.Should().Be(host.OtherHotelId);
    }

    [Fact]
    public async Task Code_of_a_soft_deleted_room_type_can_be_used_again()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var spare = await host.AddRoomTypeAsync(host.HotelId, "SUI", "Suite");
        await host.Dispatcher.Send(new DeleteRoomTypeRequest(spare.Id));

        var recreated = await host.Dispatcher.Send(Request("SUI"));

        recreated.Id.Should().NotBe(spare.Id);
        recreated.Code.Should().Be("SUI");
    }

    [Fact]
    public async Task Response_derives_currency_from_the_hotel_and_starts_with_no_rooms()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request("EZM", "Einzelzimmer"));

        created.Currency.Should().Be("EUR", "para birimi otelin ayarindan gelir, istekten DEGIL");
        created.RoomCount.Should().Be(0);
        created.BasePrice.Should().Be(99.90m);
    }

    [Fact]
    public async Task Room_count_reflects_only_the_rooms_that_are_not_deleted()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "501");
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "502");
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "503", isDeleted: true);

        var detail = await host.Dispatcher.Send(new GetRoomTypeByIdRequest(host.RoomTypeId));

        detail.RoomCount.Should().Be(2);
    }

    [Fact]
    public async Task Negative_base_price_is_rejected_by_validation()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new CreateRoomTypeRequest
        {
            Code = "NEG",
            Name = "Testzimmer",
            BasePrice = -1m,
            Capacity = 2
        });

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey(nameof(CreateRoomTypeRequest.BasePrice));
    }

    [Fact]
    public async Task Head_office_user_without_an_active_hotel_cannot_create_a_room_type()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        host.CurrentUser.HotelId = null;
        host.CurrentUser.CanAccessAllHotels = true;

        var act = async () => await host.Dispatcher.Send(Request("HQX"));

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey("X-Hotel-Id");
    }
}
