using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelCore.Infrastructure.Persistence.Migrations
{
    // Misafire açık (public) rezervasyon kanalının şema katmanı.
    // Kaynak: docs/architecture-public-booking.md §7 (15 kalem) + §5.2 (çakışma kısıtları),
    //         docs/api-contracts-public-booking.md.
    //
    // ---- Neden TEK migration ---------------------------------------------------------------
    // Kalemler birbirine bağımlıdır: BookingHolds tablosu olmadan hold çakışma kısıtı, Hotels'in
    // public kolonları olmadan seed, PublicBookings olmadan rezervasyonun kanıt kaydı yazılamaz.
    // Bölünmüş bir seri, ARADA kalan her adımda şemayı "yarı public" bırakır; üretimde iki
    // migration arasında hata alınırsa geri dönüş yolu belirsizleşir.
    //
    // ---- İki EXCLUDE kısıtı (aşağıda ham SQL) ----------------------------------------------
    // 1) EX_Reservations_NoOverlappingStays — MEVCUT BİR AÇIĞI KAPATIR. Bugün çakışmayı yalnızca
    //    AvailabilityService'in KİLİTSİZ ön kontrolü engelliyor; iki eşzamanlı istek birbirinin
    //    henüz commit edilmemiş satırını görmediği için ikisi de kontrolü geçer ve aynı oda aynı
    //    tarihe İKİ KEZ satılır. Public kanal bunu trafikle görünür kılacaktı, ama hata public
    //    kanaldan bağımsız olarak bugün de vardır (iki resepsiyonist aynı anda kaydederse).
    // 2) EX_BookingHolds_NoOverlappingActiveHolds — aynı odayı iki misafirin aynı anda tutmasını
    //    engeller.
    //
    // ---- Kısmi predikatların IMMUTABLE olması ----------------------------------------------
    // PostgreSQL kısmi index/kısıt predikatlarında yalnızca IMMUTABLE ifadelere izin verir
    // (now(), CURRENT_DATE YASAK): predikat zamanla değişirse index sessizce tutarsızlaşır.
    // Bu yüzden:
    //   * Reservations predikatı ZAMAN İÇERMEZ — yalnızca kolon karşılaştırmaları:
    //     "Status" NOT IN ('Cancelled','NoShow') AND NOT "IsDeleted". Enum METİN olarak
    //     saklandığı için karşılaştırma metin eşitliğidir; NOT "IsDeleted" ise saf boolean.
    //     Geçmiş konaklamalar da kapsamda kalır — bu İSTENEN davranıştır: geçmişteki bir çift
    //     rezervasyon da bir çift rezervasyondur ve eski satırların kapsam dışına düşmesi kısıtı
    //     doğrulanamaz hâle getirirdi.
    //   * BookingHolds'ta "süresi dolmuş" hâli predikata YAZILAMAZ (zaman gerektirirdi), bu
    //     yüzden predikat "ConsumedAt" IS NULL ile sınırlıdır ve süre dolması FİZİKSEL SİLME ile
    //     yönetilir (bu nedenle BookingHold bilinçli olarak ISoftDeletable DEĞİLDİR).
    //
    // ---- Mevcut veride çakışma varsa -------------------------------------------------------
    // ALTER TABLE ... ADD CONSTRAINT EXCLUDE mevcut satırları DOĞRULAR; çakışma varsa 23P01 ile
    // patlar. Bu migration ön uçuş (pre-flight) denetimi yapar ve çakışan rezervasyonları
    // OKUNUR bir hata mesajında listeler. Veriyi KENDİLİĞİNDEN DÜZELTMEZ (iptal/silme/oda
    // değiştirme yapmaz): hangi misafirin taşınacağı ticari bir karardır ve otomatik bir
    // mutasyon, gerçek bir rezervasyonu sessizce yok edebilir. Migration'ın patlaması,
    // yanlış veriyle sessizce devam etmesinden iyidir.
    //
    // ---- Down davranışı ---------------------------------------------------------------------
    // Kısıtlar ve tablolar düşürülür; Hotels/HeadOffices'e eklenen kolonlar DROP edilir → public
    // kanal yapılandırması ve hukuki künye GERİ DÖNÜŞSÜZ kaybolur. BookingHolds/PublicBookings
    // tabloları da düşer; PublicBookings RIZA KANITI taşır (DSGVO Art. 7 Abs. 1) — Down üretimde
    // yalnızca bilinçli bir geri alma senaryosunda çalıştırılmalıdır.
    // btree_gist extension'ı BİLİNÇLİ olarak DROP EDİLMEZ (mevcut RatePlans kısıtı ona bağlıdır).
    /// <inheritdoc />
    public partial class AddPublicBookingChannel : Migration
    {
        /// <summary>Rezervasyon çakışma kısıtı — bu migration'ın en kritik parçası.</summary>
        private const string ReservationOverlapConstraint = "EX_Reservations_NoOverlappingStays";

        /// <summary>Aktif hold çakışma kısıtı.</summary>
        private const string BookingHoldOverlapConstraint = "EX_BookingHolds_NoOverlappingActiveHolds";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Amenities",
                table: "Hotels",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CancellationPolicy_CutoffLocalTime",
                table: "Hotels",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(18, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "CancellationPolicy_FreeCancellationDaysBeforeArrival",
                table: "Hotels",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "CancellationPolicy_LateCancellationFeePercent",
                table: "Hotels",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 90.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "CancellationPolicy_NoShowFeePercent",
                table: "Hotels",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 90.00m);

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicy_Type",
                table: "Hotels",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Flexible");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckInFromLocal",
                table: "Hotels",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(15, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckOutUntilLocal",
                table: "Hotels",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(11, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_AddressLine",
                table: "Hotels",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_City",
                table: "Hotels",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_Country",
                table: "Hotels",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_DisputeResolutionNotice",
                table: "Hotels",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_Email",
                table: "Hotels",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_LegalEntityName",
                table: "Hotels",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_LegalForm",
                table: "Hotels",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_OnlineDisputeResolutionUrl",
                table: "Hotels",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LegalProfile_ParticipatesInDisputeResolution",
                table: "Hotels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_Phone",
                table: "Hotels",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_PostalCode",
                table: "Hotels",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_RegisterCourt",
                table: "Hotels",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_RegisterNumber",
                table: "Hotels",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_RepresentedBy",
                table: "Hotels",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalProfile_SupervisoryAuthority",
                table: "Hotels",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicBookingSettings_ConfirmationMode",
                table: "Hotels",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Instant");

            migrationBuilder.AddColumn<bool>(
                name: "PublicBookingSettings_IsEnabled",
                table: "Hotels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PublicBookingSettings_MaxAdults",
                table: "Hotels",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "PublicBookingSettings_MaxAdvanceDays",
                table: "Hotels",
                type: "integer",
                nullable: false,
                defaultValue: 365);

            migrationBuilder.AddColumn<int>(
                name: "PublicBookingSettings_MaxChildren",
                table: "Hotels",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "PublicBookingSettings_MaxNights",
                table: "Hotels",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "PublicBookingSettings_MinAdvanceHours",
                table: "Hotels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PublicBookingSettings_MinNights",
                table: "Hotels",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "PublicHost",
                table: "Hotels",
                type: "character varying(253)",
                maxLength: 253,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "Hotels",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Hotels",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Europe/Berlin");

            migrationBuilder.AddColumn<string>(
                name: "VatId",
                table: "Hotels",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "HeadOffices",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckIn = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOut = table.Column<DateOnly>(type: "date", nullable: false),
                    Adults = table.Column<int>(type: "integer", nullable: false),
                    Children = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedByReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientIpHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AccommodationGross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CityTaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalGross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CancellationPolicySnapshotJson = table.Column<string>(type: "text", nullable: false),
                    OrderSummaryJson = table.Column<string>(type: "text", nullable: false),
                    SummaryHash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LegalSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    Culture = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingHolds", x => x.Id);
                    table.CheckConstraint("CK_BookingHolds_ConsumptionIsComplete", "(\"ConsumedAt\" IS NULL) = (\"ConsumedByReservationId\" IS NULL)");
                    table.CheckConstraint("CK_BookingHolds_ValidStay", "\"CheckIn\" < \"CheckOut\"");
                    table.ForeignKey(
                        name: "FK_BookingHolds_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingHolds_Reservations_ConsumedByReservationId",
                        column: x => x.ConsumedByReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingHolds_RoomTypes_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "RoomTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingHolds_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HotelImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HotelImages_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HotelLegalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Culture = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelLegalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HotelLegalDocuments_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingReference = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AccessTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Culture = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CountryOfResidence = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    EstimatedArrivalLocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    InvoiceAddress_Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceAddress_AddressLine = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InvoiceAddress_PostalCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    InvoiceAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InvoiceAddress_Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    InvoiceAddress_VatId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TermsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PrivacyNoticeAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    PrivacyNoticeVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    WithdrawalNoticeAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    WithdrawalNoticeVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BookerIsAdult = table.Column<bool>(type: "boolean", nullable: false),
                    MarketingOptIn = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentRecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrderButtonLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SummaryHash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OrderSummaryJson = table.Column<string>(type: "text", nullable: false),
                    PriceSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CancellationPolicySnapshotJson = table.Column<string>(type: "text", nullable: false),
                    LegalSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ConfirmationMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConfirmationSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmationDocumentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ConfirmationDocumentVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ConfirmationCulture = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicBookings_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicBookings_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomTypeImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTypeImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomTypeImages_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomTypeImages_RoomTypes_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "RoomTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservations_ValidStay",
                table: "Reservations",
                sql: "\"CheckIn\" < \"CheckOut\"");

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_PublicHost",
                table: "Hotels",
                column: "PublicHost",
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_PublicSlug",
                table: "Hotels",
                column: "PublicSlug",
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hotels_PublicBookingSettings",
                table: "Hotels",
                sql: "\"PublicBookingSettings_MinNights\" >= 1 AND \"PublicBookingSettings_MaxNights\" >= \"PublicBookingSettings_MinNights\" AND \"PublicBookingSettings_MaxAdvanceDays\" >= 1 AND \"PublicBookingSettings_MinAdvanceHours\" >= 0 AND \"PublicBookingSettings_MaxAdults\" >= 1 AND \"PublicBookingSettings_MaxChildren\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_HeadOffices_PublicSlug",
                table: "HeadOffices",
                column: "PublicSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_ConsumedAt",
                table: "BookingHolds",
                column: "ConsumedAt",
                filter: "\"ConsumedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_ConsumedByReservationId",
                table: "BookingHolds",
                column: "ConsumedByReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_ExpiresAt",
                table: "BookingHolds",
                column: "ExpiresAt",
                filter: "\"ConsumedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_HotelId_RoomTypeId_CheckIn_CheckOut",
                table: "BookingHolds",
                columns: new[] { "HotelId", "RoomTypeId", "CheckIn", "CheckOut" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_RoomId",
                table: "BookingHolds",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_RoomTypeId",
                table: "BookingHolds",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_TokenHash",
                table: "BookingHolds",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HotelImages_HotelId_SortOrder",
                table: "HotelImages",
                columns: new[] { "HotelId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HotelLegalDocuments_HotelId_Key_Culture",
                table: "HotelLegalDocuments",
                columns: new[] { "HotelId", "Key", "Culture" },
                filter: "\"IsActive\" AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_HotelLegalDocuments_HotelId_Key_Culture_Version",
                table: "HotelLegalDocuments",
                columns: new[] { "HotelId", "Key", "Culture", "Version" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookings_AccessTokenExpiresAt",
                table: "PublicBookings",
                column: "AccessTokenExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookings_AccessTokenHash",
                table: "PublicBookings",
                column: "AccessTokenHash",
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookings_BookingReference",
                table: "PublicBookings",
                column: "BookingReference",
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookings_HotelId",
                table: "PublicBookings",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicBookings_ReservationId",
                table: "PublicBookings",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypeImages_HotelId",
                table: "RoomTypeImages",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypeImages_RoomTypeId_SortOrder",
                table: "RoomTypeImages",
                columns: new[] { "RoomTypeId", "SortOrder" });

            DropBackfillDefaults(migrationBuilder);
            AddSlugFormatConstraints(migrationBuilder);
            EnsureNoOverlappingReservations(migrationBuilder);
            AddOverlapConstraints(migrationBuilder);
        }

        /// <summary>
        /// Slug/host <b>biçim</b> kısıtları.
        /// <para>
        /// <b>Neden ham SQL (modelde değil):</b> PostgreSQL'in <c>~</c> regex operatörü
        /// SQLite'ta yoktur; kısıt EF modeline konursa handler testlerinin
        /// <c>EnsureCreated</c> ile kurduğu SQLite şeması derlenemez ("near ~: syntax error").
        /// Aynı gerekçe EXCLUDE kısıtları için de geçerlidir — sağlayıcıya özgü kısıtlar bu
        /// projede modele değil migration'a yazılır.
        /// </para>
        /// <para>
        /// <b>Neden var:</b> slug bir URL parçasıdır. Büyük harf, boşluk veya Unicode içeren bir
        /// değer yazıldığı anda otel misafir sitesinde <b>404</b> olur ve hata çok sonra,
        /// üretimde fark edilir. Desen sözleşmedeki ile birebir aynıdır
        /// (api-contracts-public-booking.md §10). Uygulama katmanı bunu ayrıca doğrular; kısıt
        /// "hangi yoldan yazılırsa yazılsın" garantisidir.
        /// </para>
        /// </summary>
        private static void AddSlugFormatConstraints(MigrationBuilder migrationBuilder)
        {
            const string SlugPattern = "^[a-z0-9](?:[a-z0-9-]{1,58}[a-z0-9])$";

            // İptal ücreti yüzdeleri de burada: EF'in SQLite sağlayıcısı decimal'i TEXT olarak
            // saklar, dolayısıyla modelde duran bir "BETWEEN 0 AND 100" handler testlerinin
            // şemasında METİN karşılaştırmasına dönüşür ve her satırı reddeder. Kısıt yalnızca
            // PostgreSQL'de anlamlıdır. Yüzde 0–100 dışına çıkarsa iptal ücreti konaklama
            // tutarını aşar veya negatif olur; ikisi de misafire yanlış tutar gösterir.
            migrationBuilder.Sql("""
                ALTER TABLE "Hotels"
                    ADD CONSTRAINT "CK_Hotels_CancellationPolicy"
                    CHECK ("CancellationPolicy_FreeCancellationDaysBeforeArrival" >= 0
                       AND "CancellationPolicy_LateCancellationFeePercent" BETWEEN 0 AND 100
                       AND "CancellationPolicy_NoShowFeePercent" BETWEEN 0 AND 100);
                """);

            migrationBuilder.Sql($"""
                ALTER TABLE "Hotels"
                    ADD CONSTRAINT "CK_Hotels_PublicSlugFormat"
                    CHECK ("PublicSlug" IS NULL OR "PublicSlug" ~ '{SlugPattern}');
                """);

            // DNS büyük/küçük harf duyarsızdır, ama host -> slug eşlemesi metin
            // karşılaştırmasıyla yapılır; karışık kasa yazılırsa eşleme sessizce başarısız olur.
            migrationBuilder.Sql("""
                ALTER TABLE "Hotels"
                    ADD CONSTRAINT "CK_Hotels_PublicHostFormat"
                    CHECK ("PublicHost" IS NULL OR "PublicHost" ~ '^[a-z0-9.-]+$');
                """);

            migrationBuilder.Sql($"""
                ALTER TABLE "HeadOffices"
                    ADD CONSTRAINT "CK_HeadOffices_PublicSlugFormat"
                    CHECK ("PublicSlug" IS NULL OR "PublicSlug" ~ '{SlugPattern}');
                """);
        }

        /// <summary>
        /// Yukarıdaki <c>defaultValue</c>'lar <b>yalnızca mevcut satırları doldurmak</b> içindir
        /// (NOT NULL kolon boş bir tabloya olmayan bir değerle eklenemez). Kalıcı DEFAULT
        /// bırakılmaz, iki nedenle:
        /// <list type="number">
        ///   <item><description>Model anlık görüntüsünde (snapshot) bu kolonların DEFAULT'u
        ///   YOKTUR; DB'de bırakılırsa şema ile model kalıcı olarak ayrışır ve bir sonraki
        ///   <c>migrations add</c> gereksiz bir <c>AlterColumn</c> üretir.</description></item>
        ///   <item><description>Kalıcı DEFAULT, uygulamanın <b>unuttuğu</b> bir kolonu sessizce
        ///   doldurur. Değerlerin sahibi domain'deki property initializer'larıdır; veritabanının
        ///   ikinci bir varsayılan seti tutması, "otelin saat dilimi neden Berlin?" sorusunun iki
        ///   farklı cevabı olması demektir.</description></item>
        /// </list>
        /// </summary>
        private static void DropBackfillDefaults(MigrationBuilder migrationBuilder)
        {
            string[] hotelColumns =
            [
                "TimeZoneId",
                "CheckInFromLocal",
                "CheckOutUntilLocal",
                "CancellationPolicy_Type",
                "CancellationPolicy_CutoffLocalTime",
                "CancellationPolicy_FreeCancellationDaysBeforeArrival",
                "CancellationPolicy_LateCancellationFeePercent",
                "CancellationPolicy_NoShowFeePercent",
                "PublicBookingSettings_IsEnabled",
                "PublicBookingSettings_ConfirmationMode",
                "PublicBookingSettings_MinNights",
                "PublicBookingSettings_MaxNights",
                "PublicBookingSettings_MaxAdvanceDays",
                "PublicBookingSettings_MinAdvanceHours",
                "PublicBookingSettings_MaxAdults",
                "PublicBookingSettings_MaxChildren",
                "LegalProfile_ParticipatesInDisputeResolution"
            ];

            foreach (var column in hotelColumns)
            {
                migrationBuilder.Sql($"""ALTER TABLE "Hotels" ALTER COLUMN "{column}" DROP DEFAULT;""");
            }
        }

        /// <summary>
        /// <b>Ön uçuş denetimi.</b> <c>ADD CONSTRAINT ... EXCLUDE</c> mevcut satırları doğrular;
        /// veride çift rezervasyon varsa migration ham bir <c>23P01</c> ile patlar ve operatör
        /// <i>hangi</i> rezervasyonların çakıştığını göremez.
        /// <para>
        /// Bu blok çakışmaları <b>önce</b> bulur ve okunabilir bir mesajda (en fazla 20 çift,
        /// rezervasyon numaralarıyla) listeler. <b>Veriyi düzeltmez</b>: hangi misafirin
        /// taşınacağı/iptal edileceği ticari bir karardır ve otomatik bir mutasyon gerçek bir
        /// satışı sessizce yok edebilir. Migration'ın durması, sessizce yanlış davranmasından
        /// iyidir.
        /// </para>
        /// <para>
        /// Kesişim koşulu kısıtla <b>birebir</b> aynıdır (yarı açık aralık, aynı bloke edici
        /// durum kümesi, silinmişler hariç), yoksa denetim ile kısıt farklı şeyleri ölçerdi.
        /// </para>
        /// </summary>
        private static void EnsureNoOverlappingReservations(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    conflict_count integer;
                    conflict_sample text;
                BEGIN
                    WITH overlapping AS (
                        SELECT a."ReservationNumber" AS left_number,
                               b."ReservationNumber" AS right_number,
                               a."RoomId"            AS room_id,
                               a."CheckIn"           AS left_check_in,
                               a."CheckOut"          AS left_check_out
                        FROM "Reservations" a
                        JOIN "Reservations" b
                          ON a."RoomId" = b."RoomId"
                         AND a."Id" < b."Id"
                         AND a."CheckIn" < b."CheckOut"
                         AND b."CheckIn" < a."CheckOut"
                        WHERE a."Status" NOT IN ('Cancelled', 'NoShow')
                          AND b."Status" NOT IN ('Cancelled', 'NoShow')
                          AND NOT a."IsDeleted"
                          AND NOT b."IsDeleted"
                    ),
                    numbered AS (
                        SELECT overlapping.*, row_number() OVER () AS rn FROM overlapping
                    )
                    SELECT count(*),
                           string_agg(
                               format('%s <-> %s (oda %s, %s..%s)',
                                      left_number, right_number, room_id,
                                      left_check_in, left_check_out),
                               E'\n') FILTER (WHERE rn <= 20)
                    INTO conflict_count, conflict_sample
                    FROM numbered;

                    IF conflict_count > 0 THEN
                        RAISE EXCEPTION
                            'EX_Reservations_NoOverlappingStays eklenemedi: mevcut veride % cakisan rezervasyon cifti var. Ornekler: %',
                            conflict_count, conflict_sample
                        USING HINT = 'Cakisan rezervasyonlari once cozun (iptal edin veya baska odaya tasiyin), sonra migration''i tekrar calistirin. Bu migration veriyi KENDILIGINDEN DEGISTIRMEZ.';
                    END IF;
                END $$;
                """);

        /// <summary>
        /// İki aralık dışlama kısıtı. Ayrıntılı gerekçe sınıf başlığındaki nottadır.
        /// </summary>
        private static void AddOverlapConstraints(MigrationBuilder migrationBuilder)
        {
            // gist erişim yöntemi uuid üzerinde "=" operatörünü ancak btree_gist ile destekler.
            // IF NOT EXISTS: extension zaten RatePlans kısıtı için kurulmuştu.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // Anahtarın parçaları:
            //   "RoomId" WITH =   -> çakışma yalnızca AYNI oda içinde aranır. HotelId BİLİNÇLİ
            //                        olarak yoktur: oda zaten tek bir otele aittir ve HotelId
            //                        eklemek kısıtı yalnızca ZAYIFLATIRDI (HotelId'si tutarsız
            //                        yazılmış iki satır çakışmıyor sayılırdı).
            //   daterange(..., '[)') -> YARI AÇIK aralık, mevcut [CheckIn, CheckOut) semantiğiyle
            //                        birebir aynı: bir konaklamanın çıkış günü, aynı odada başka
            //                        bir konaklamanın giriş günü olabilir (ardışık konaklama).
            //                        RatePlans'teki '[]' KAPALI aralıktan farkı budur ve
            //                        farklılık kasıtlıdır.
            //   WHERE (...)        -> kısmi kısıt. Cancelled/NoShow oda takviminden düşer
            //                        (AvailabilityQuery.IsBlocking ile AYNI küme), soft-delete
            //                        edilmiş satır hiç yoktur. Predikat SALT KOLON + SABİT
            //                        karşılaştırmasıdır, yani IMMUTABLE'dır; zaman ifadesi
            //                        (now(), CURRENT_DATE) PostgreSQL tarafından zaten
            //                        reddedilirdi ve burada gerekli de değildir.
            migrationBuilder.Sql($"""
                ALTER TABLE "Reservations"
                    ADD CONSTRAINT "{ReservationOverlapConstraint}"
                    EXCLUDE USING gist (
                        "RoomId" WITH =,
                        daterange("CheckIn", "CheckOut", '[)') WITH &&
                    ) WHERE ("Status" NOT IN ('Cancelled', 'NoShow') AND NOT "IsDeleted");
                """);

            // Hold'lar için aynı mekanizma. Predikat "ConsumedAt" IS NULL ile sınırlıdır:
            //   * tüketilmiş hold artık odayı bloke etmemelidir (odayı rezervasyonun kendisi ve
            //     yukarıdaki kısıt korur),
            //   * "süresi dolmuş" hâli predikata YAZILAMAZ (zaman ifadesi immutable değildir);
            //     süre dolması FİZİKSEL SİLME ile yönetilir — hold oluşturma handler'ı aynı
            //     transaction'da kesişen süresi dolmuş hold'ları siler, ayrıca bir HostedService
            //     periyodik olarak süpürür.
            migrationBuilder.Sql($"""
                ALTER TABLE "BookingHolds"
                    ADD CONSTRAINT "{BookingHoldOverlapConstraint}"
                    EXCLUDE USING gist (
                        "RoomId" WITH =,
                        daterange("CheckIn", "CheckOut", '[)') WITH &&
                    ) WHERE ("ConsumedAt" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Kısıtlar ÖNCE düşürülür. BookingHolds kısıtı tabloyla birlikte zaten giderdi ama
            // sıra Up'ın tersi olsun diye açıkça yazılır; Reservations kısıtı ise tablo ayakta
            // kaldığı için MUTLAKA burada düşürülmelidir. IF EXISTS: Down'ın kısmen uygulanmış
            // bir şema üzerinde de çalışabilmesi için.
            migrationBuilder.Sql(
                $"""ALTER TABLE "BookingHolds" DROP CONSTRAINT IF EXISTS "{BookingHoldOverlapConstraint}";""");
            migrationBuilder.Sql(
                $"""ALTER TABLE "Reservations" DROP CONSTRAINT IF EXISTS "{ReservationOverlapConstraint}";""");

            migrationBuilder.DropTable(
                name: "BookingHolds");

            migrationBuilder.DropTable(
                name: "HotelImages");

            migrationBuilder.DropTable(
                name: "HotelLegalDocuments");

            migrationBuilder.DropTable(
                name: "PublicBookings");

            migrationBuilder.DropTable(
                name: "RoomTypeImages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservations_ValidStay",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_PublicHost",
                table: "Hotels");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_PublicSlug",
                table: "Hotels");

            migrationBuilder.Sql(
                """ALTER TABLE "Hotels" DROP CONSTRAINT IF EXISTS "CK_Hotels_CancellationPolicy";""");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Hotels_PublicBookingSettings",
                table: "Hotels");

            // Biçim kısıtları ham SQL ile eklendiği için ham SQL ile düşürülür (EF modeli onları
            // tanımaz — bkz. AddSlugFormatConstraints).
            migrationBuilder.Sql(
                """ALTER TABLE "Hotels" DROP CONSTRAINT IF EXISTS "CK_Hotels_PublicHostFormat";""");
            migrationBuilder.Sql(
                """ALTER TABLE "Hotels" DROP CONSTRAINT IF EXISTS "CK_Hotels_PublicSlugFormat";""");
            migrationBuilder.Sql(
                """ALTER TABLE "HeadOffices" DROP CONSTRAINT IF EXISTS "CK_HeadOffices_PublicSlugFormat";""");

            migrationBuilder.DropIndex(
                name: "IX_HeadOffices_PublicSlug",
                table: "HeadOffices");

            migrationBuilder.DropColumn(
                name: "Amenities",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy_CutoffLocalTime",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy_FreeCancellationDaysBeforeArrival",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy_LateCancellationFeePercent",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy_NoShowFeePercent",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy_Type",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CheckInFromLocal",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CheckOutUntilLocal",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_AddressLine",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_City",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_Country",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_DisputeResolutionNotice",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_Email",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_LegalEntityName",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_LegalForm",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_OnlineDisputeResolutionUrl",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_ParticipatesInDisputeResolution",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_Phone",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_PostalCode",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_RegisterCourt",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_RegisterNumber",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_RepresentedBy",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "LegalProfile_SupervisoryAuthority",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicBookingSettings_ConfirmationMode",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicBookingSettings_IsEnabled",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicBookingSettings_MaxAdults",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicBookingSettings_MaxAdvanceDays",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicBookingSettings_MaxChildren",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicBookingSettings_MaxNights",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicBookingSettings_MinAdvanceHours",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicBookingSettings_MinNights",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicHost",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "VatId",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "HeadOffices");
        }
    }
}
