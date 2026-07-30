using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Application.Features.Reservations.Create;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Tests.Support;

/// <summary>
/// Rezervasyon modulu handler testleri icin veri sahnesi (oteller, oda tipleri, odalar,
/// misafirler, fiyat planlari).
/// <para>
/// Ortak altyapi (SQLite <c>:memory:</c>, gercek <c>AppDbContext</c>, <c>AddApplication()</c>
/// boru hatti) <see cref="ApplicationTestHost"/> icindedir.
/// </para>
/// <para>
/// <b>Faturalama neden burada YOK:</b> fatura okuma yolu (<c>InvoiceReader</c>) odemeleri
/// <c>PaidAt</c>, denetim izini <c>PerformedAt</c> ile — yani <c>DateTimeOffset</c> kolonlarina
/// gore — siralar. EF Core'un SQLite saglayicisi bu <c>ORDER BY</c>'i CEVIREMEZ, dolayisiyla
/// <b>her</b> fatura yazma ucu bu host uzerinde <c>NotSupportedException</c> ile patlar.
/// Faturalama testleri bu yuzden gercek PostgreSQL'e karsi
/// <c>HotelCore.Api.IntegrationTests/Invoices</c> altinda kosar; burada sahte bir sonucla yesil
/// gosterilmez.
/// </para>
/// </summary>
internal sealed class BookingModuleTestHost : ApplicationTestHost
{
    /// <summary>Kurtaxe: kisi basi gecelik tutar (12,00 / 24,00 senaryosunun tabani).</summary>
    public const decimal CityTaxPerPersonNight = 3.00m;

    /// <summary>A otelindeki oda tipinin liste fiyati (fiyat plani yoksa kullanilir).</summary>
    public const decimal BasePrice = 120m;

    /// <summary>Elle yazilan rezervasyon numaralarinin sirasi (otel ici benzersizlik icin).</summary>
    private int _seededReservationCount;

    private BookingModuleTestHost()
    {
    }

    /// <summary>Iki otelin bagli oldugu Head Office.</summary>
    public Guid HeadOfficeId { get; private set; }

    /// <summary>A oteli — testlerin varsayilan aktif oteli.</summary>
    public Guid HotelId { get; private set; }

    /// <summary>B oteli — tenant izolasyonu senaryolari icin.</summary>
    public Guid OtherHotelId { get; private set; }

    /// <summary>A otelindeki oda tipi (<c>DBL</c>, kapasite 4).</summary>
    public Guid RoomTypeId { get; private set; }

    /// <summary>B otelindeki oda tipi.</summary>
    public Guid OtherHotelRoomTypeId { get; private set; }

    /// <summary>A otelindeki 101 numarali oda.</summary>
    public Guid RoomId { get; private set; }

    /// <summary>A otelindeki 102 numarali oda (ikinci oda gerektiren senaryolar icin).</summary>
    public Guid SecondRoomId { get; private set; }

    /// <summary>A otelindeki servis disi oda (199).</summary>
    public Guid OutOfOrderRoomId { get; private set; }

    /// <summary>B otelindeki oda.</summary>
    public Guid OtherHotelRoomId { get; private set; }

    /// <summary>A otelindeki misafir.</summary>
    public Guid GuestId { get; private set; }

    /// <summary>B otelindeki misafir.</summary>
    public Guid OtherHotelGuestId { get; private set; }

    /// <summary>Sabit saatin gosterdigi gun (2026-06-15).</summary>
    public DateOnly Today => Clock.Today;

    public static async Task<BookingModuleTestHost> CreateAsync()
    {
        var host = new BookingModuleTestHost();

        try
        {
            await host.InitialiseAsync();

            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Veri kurulumu (API/handler kullanmadan dogrudan veritabanina)
    // ---------------------------------------------------------------------------------------

    public async Task<Guid> AddRoomAsync(
        Guid hotelId,
        Guid roomTypeId,
        string number,
        bool isOutOfOrder = false,
        HousekeepingStatus status = HousekeepingStatus.Clean)
    {
        var room = new Room
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Number = number,
            Floor = 1,
            HousekeepingStatus = isOutOfOrder ? HousekeepingStatus.OutOfOrder : status,
            IsOutOfOrder = isOutOfOrder
        };

        Database.Rooms.Add(room);
        await SaveAndDetachAsync();

        return room.Id;
    }

    public async Task<Guid> AddGuestAsync(
        Guid hotelId,
        string firstName = "Anna",
        string lastName = "Muster",
        string? culture = null)
    {
        var guest = new Guest
        {
            HotelId = hotelId,
            FirstName = firstName,
            LastName = lastName,
            Culture = culture
        };

        Database.Guests.Add(guest);
        await SaveAndDetachAsync();

        return guest.Id;
    }

    /// <summary>Fiyat plani ekler (cakisma on kontrolunu ATLAYARAK — dogrudan veritabanina).</summary>
    public async Task<Guid> AddRatePlanAsync(
        Guid roomTypeId,
        string name,
        decimal price,
        DateOnly validFrom,
        DateOnly validTo,
        ReservationChannel? channel = null,
        bool isActive = true,
        Guid? hotelId = null)
    {
        var plan = new RatePlan
        {
            HotelId = hotelId ?? HotelId,
            RoomTypeId = roomTypeId,
            Name = name,
            Price = price,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Channel = channel,
            IsActive = isActive
        };

        Database.RatePlans.Add(plan);
        await SaveAndDetachAsync();

        return plan.Id;
    }

    /// <summary>
    /// Rezervasyonu <b>dogrudan</b> veritabanina yazar (musaitlik/fiyat boru hattini atlar).
    /// Cakisma ve durum senaryolarinin baslangic verisini kurmak icin.
    /// </summary>
    public async Task<Guid> AddReservationAsync(
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        ReservationStatus status = ReservationStatus.Confirmed,
        Guid? hotelId = null,
        Guid? guestId = null,
        int adults = 2,
        int children = 0,
        decimal totalAmount = 0m)
    {
        var reservation = new Reservation
        {
            HotelId = hotelId ?? HotelId,
            RoomId = roomId,
            GuestId = guestId ?? GuestId,
            // Uretilen numara ile catismaz: seed numaralari 9xxxx bloguna yazilir.
            ReservationNumber = $"RES-2026-9{++_seededReservationCount:0000}",
            CheckIn = checkIn,
            CheckOut = checkOut,
            Adults = adults,
            Children = children,
            Status = status,
            TotalAmount = totalAmount
        };

        Database.Reservations.Add(reservation);
        await SaveAndDetachAsync();

        return reservation.Id;
    }

    /// <summary>Rezervasyonu <b>uretim yolundan</b> (handler + fiyatlama + folio) olusturur.</summary>
    public Task<ReservationResponse> CreateReservationAsync(
        DateOnly checkIn,
        DateOnly checkOut,
        Guid? roomId = null,
        Guid? guestId = null,
        int adults = 2,
        int children = 0,
        ReservationChannel channel = ReservationChannel.Direct,
        ReservationStatus? status = null) =>
        Dispatcher.Send(new CreateReservationRequest
        {
            RoomId = roomId ?? RoomId,
            GuestId = guestId ?? GuestId,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Adults = adults,
            Children = children,
            Channel = channel,
            Status = status
        });

    // ---------------------------------------------------------------------------------------
    // Dogrulama yardimcilari (global query filter'i atlayarak ham satir okur)
    // ---------------------------------------------------------------------------------------

    public Task<Reservation?> FindReservationAsync(Guid id) =>
        Database.Reservations.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(reservation => reservation.Id == id);

    public Task<Room?> FindRoomAsync(Guid id) =>
        Database.Rooms.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(room => room.Id == id);

    protected override async Task SeedAsync()
    {
        var headOffice = new HeadOffice { BrandName = "HotelCore Booking Test", DefaultCulture = "de" };
        var hotelA = NewHotel(headOffice.Id, "Test Hotel A");
        var hotelB = NewHotel(headOffice.Id, "Test Hotel B");

        Database.HeadOffices.Add(headOffice);
        Database.Hotels.Add(hotelA);
        Database.Hotels.Add(hotelB);
        await SaveAndDetachAsync();

        HeadOfficeId = headOffice.Id;
        HotelId = hotelA.Id;
        OtherHotelId = hotelB.Id;

        // Kapasite 4: "2 yetiskin + 2 cocuk" Kurtaxe senaryosu icin gereklidir.
        RoomTypeId = (await AddRoomTypeAsync(HotelId, "DBL", "Doppelzimmer", BasePrice, capacity: 4)).Id;
        OtherHotelRoomTypeId =
            (await AddRoomTypeAsync(OtherHotelId, "TWN", "Zweibettzimmer B", 100m, capacity: 2)).Id;

        RoomId = await AddRoomAsync(HotelId, RoomTypeId, "101");
        SecondRoomId = await AddRoomAsync(HotelId, RoomTypeId, "102");
        OutOfOrderRoomId = await AddRoomAsync(HotelId, RoomTypeId, "199", isOutOfOrder: true);
        OtherHotelRoomId = await AddRoomAsync(OtherHotelId, OtherHotelRoomTypeId, "B01");

        GuestId = await AddGuestAsync(HotelId);
        OtherHotelGuestId = await AddGuestAsync(OtherHotelId, "Bert", "Fremd");

        // Varsayilan baglam: A otelinin kullanicisi (Head Office bypass'i kapali).
        CurrentUser.HotelId = HotelId;
        CurrentUser.HeadOfficeId = HeadOfficeId;
    }

    private async Task<RoomType> AddRoomTypeAsync(
        Guid hotelId,
        string code,
        string name,
        decimal basePrice,
        int capacity)
    {
        var roomType = new RoomType
        {
            HotelId = hotelId,
            Code = code,
            Name = name,
            BasePrice = basePrice,
            Capacity = capacity
        };

        Database.RoomTypes.Add(roomType);
        await SaveAndDetachAsync();

        return roomType;
    }

    private static Hotel NewHotel(Guid headOfficeId, string name) => new()
    {
        HeadOfficeId = headOfficeId,
        Name = name,
        City = "Berlin",
        Country = Country.DE,
        Currency = "EUR",
        DefaultCulture = "de",
        TaxProfile = new TaxProfile
        {
            VatRate = 19m,
            ReducedVatRate = 7m,
            CityTaxEnabled = true,
            CityTaxPerPersonNight = CityTaxPerPersonNight,
            CityTaxExemptChildren = false,
            CityTaxChildAgeLimit = 18
        }
    };
}
