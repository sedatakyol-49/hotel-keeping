using AwesomeAssertions;
using HotelCore.Application.Common.Interfaces;
using NSubstitute;

namespace HotelCore.Application.Tests.Common;

/// <summary>
/// <see cref="ICurrentUser"/> ve <see cref="IDateTimeProvider"/> portlarinin test edilebilirlik
/// sozlesmesi. Handler'lar yazildiginda bu portlar NSubstitute ile taklit edilecek; burada
/// taklit edilebilirlikleri ve "kimliksiz istek" senaryosunun guvenli varsayilani dogrulanir
/// (kimlik yoksa hicbir tenant satiri gorunmemeli — bkz. AppDbContext global query filter).
/// </summary>
public sealed class IdentityPortTests
{
    [Fact]
    public void An_unconfigured_current_user_represents_an_anonymous_request()
    {
        var currentUser = Substitute.For<ICurrentUser>();

        currentUser.IsAuthenticated.Should().BeFalse();
        currentUser.UserId.Should().BeNull();
        currentUser.HotelId.Should().BeNull();
        currentUser.HeadOfficeId.Should().BeNull();
        // Kritik: bypass varsayilan olarak KAPALI olmalidir.
        currentUser.CanAccessAllHotels.Should().BeFalse();
    }

    [Fact]
    public void A_head_office_user_can_be_faked_with_the_all_hotels_bypass()
    {
        var headOfficeId = Guid.NewGuid();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(Guid.NewGuid());
        currentUser.HeadOfficeId.Returns(headOfficeId);
        currentUser.CanAccessAllHotels.Returns(true);
        currentUser.IsAuthenticated.Returns(true);

        currentUser.CanAccessAllHotels.Should().BeTrue();
        currentUser.HeadOfficeId.Should().Be(headOfficeId);
        currentUser.HotelId.Should().BeNull("Head Office konsolide gorunumde tek bir otele bagli degildir");
    }

    [Fact]
    public void A_hotel_scoped_user_can_be_faked_with_permissions()
    {
        var hotelId = Guid.NewGuid();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.HotelId.Returns(hotelId);
        currentUser.IsAuthenticated.Returns(true);
        currentUser.Permissions.Returns(new[] { "Reservations.View", "Invoices.Create" });

        currentUser.HotelId.Should().Be(hotelId);
        currentUser.CanAccessAllHotels.Should().BeFalse();
        currentUser.Permissions.Should().Contain("Invoices.Create");
    }

    [Fact]
    public void The_clock_port_can_be_frozen_for_deterministic_tests()
    {
        var frozen = new DateTimeOffset(2026, 7, 29, 8, 30, 0, TimeSpan.Zero);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(frozen);

        clock.UtcNow.Should().Be(frozen);
        clock.UtcNow.Offset.Should().Be(TimeSpan.Zero, "tum zaman damgalari UTC saklanir");
    }
}
