using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Infrastructure.Persistence.Seed;

/// <summary>
/// Kurgusal Berlin şehir otelinin <b>misafire açık kanal</b> demo verisi: slug, saat dilimi,
/// künye (Impressum), iptal politikası, hukuki belgeler, görsel yer tutucuları, Almanca oda tipi
/// metinleri ve web'e uygulanabilir bir fiyat planı.
///
/// <para><b>Gerçek otel verisi DEĞİLDİR.</b> Tüzel kişilik adı, sicil numarası, USt-IdNr. ve
/// adres <b>uydurmadır</b>; yalnızca misafir sitesinin uçtan uca demo edilebilmesi ve hukuki
/// alanların "hardcode değil, veritabanından" geldiğinin gösterilmesi içindir. Bir müşteri
/// kurulumunda bu blok hiç çalışmaz (<c>includeDevelopmentData = false</c>).</para>
///
/// <para><b>Görseller:</b> bu fazda yükleme/CDN boru hattı yoktur (architecture-public-booking.md
/// §12). Seed <b>köke göreli</b> yollar yazar (<c>/assets/demo/...</c>) — dış bir kaynağa
/// bağlanmaz, böylece demo çevrimdışı da çalışır ve kırık bir dış bağlantı üretmez.
/// <b>Dosyalar gerçekten vardır:</b> misafir uygulamasının
/// <c>projects/guest-web/public/assets/demo/berlin-mitte/</c> klasöründe, arayüzün kendi yer
/// tutucu diliyle (kâğıt zemin, 1px cetvel, çapraz iki çizgi) çizilmiş SVG'lerdir. Fotoğraf
/// taklidi <b>değildir</b> ve öyle görünmezler; ama ölçüleri gerçektir, dolayısıyla
/// <c>width</c>/<c>height</c> → CLS ve <c>alt</c> → WCAG yolları uçtan uca çalışır. Daha önce
/// yollar var dosya yoktu: her sayfa yüklemesi birkaç 404 üretiyordu ve bir demo sırasında bu,
/// gerçek bir entegrasyon hatasından ayırt edilemez.</para>
///
/// <para><b>İdempotentlik:</b> otel yapılandırması yalnızca <c>PublicSlug</c> boşken uygulanır
/// (yani kanal ilk kez açılırken); belgeler, görseller, çeviriler ve fiyat planları doğal
/// anahtarlarıyla kontrol edilir. Tekrar çalıştırmak hiçbir satırı çoğaltmaz.</para>
/// </summary>
internal static class PublicChannelSeeder
{
    /// <summary>Misafir sitesindeki otel URL anahtarı.</summary>
    private const string HotelSlug = "berlin-mitte";

    /// <summary>Marka sitesinin URL anahtarı.</summary>
    private const string BrandSlug = "hotelcore-demo-group";

    /// <summary>Demo hukuki metinlerin yayın versiyonu (opak metin, tarih tipi değildir).</summary>
    private const string LegalVersion = "2026-07-01";

    private const string ImageRoot = "/assets/demo/berlin-mitte";

    public static async Task SeedAsync(
        AppDbContext context,
        Guid headOfficeId,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        await SeedBrandSlugAsync(context, headOfficeId, cancellationToken).ConfigureAwait(false);

        var configured = await ConfigureHotelAsync(context, hotelId, cancellationToken).ConfigureAwait(false);

        await SeedLegalDocumentsAsync(context, hotelId, cancellationToken).ConfigureAwait(false);
        await SeedHotelImagesAsync(context, hotelId, cancellationToken).ConfigureAwait(false);
        await SeedRoomTypeContentAsync(context, hotelId, configured, cancellationToken).ConfigureAwait(false);
        await SeedWebRatePlansAsync(context, hotelId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SeedBrandSlugAsync(
        AppDbContext context,
        Guid headOfficeId,
        CancellationToken cancellationToken)
    {
        var headOffice = await context.HeadOffices
            .FirstOrDefaultAsync(candidate => candidate.Id == headOfficeId, cancellationToken)
            .ConfigureAwait(false);

        if (headOffice is null || headOffice.PublicSlug is not null)
        {
            return;
        }

        headOffice.PublicSlug = BrandSlug;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Otelin public kanal yapılandırması. <b>Yalnızca bir kez</b> uygulanır: <c>PublicSlug</c>
    /// doluysa geliştirici/müşteri ayarları ezilmez.
    /// </summary>
    /// <returns>Yapılandırma bu çağrıda uygulandıysa <c>true</c>.</returns>
    private static async Task<bool> ConfigureHotelAsync(
        AppDbContext context,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        var hotel = await context.Hotels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == hotelId, cancellationToken)
            .ConfigureAwait(false);

        if (hotel is null || hotel.PublicSlug is not null)
        {
            return false;
        }

        hotel.PublicSlug = HotelSlug;
        hotel.TimeZoneId = "Europe/Berlin";
        hotel.CheckInFromLocal = new TimeOnly(15, 0);
        hotel.CheckOutUntilLocal = new TimeOnly(11, 0);

        // Steuernummer ve USt-IdNr. iki AYRI numaradır; demo verisi bu ayrımı gösterir.
        hotel.TaxNumber = "37/104/52816";
        hotel.VatId = "DE289176543";

        // "Musterstraße" bir yer tutucudur ve Impressum'da yer tutucu bir adres göstermek
        // demonun anlamını bozar; kanal açılırken gerçekçi bir Berlin-Mitte adresine geçilir.
        hotel.AddressLine = "Chausseestraße 5";
        hotel.PostalCode = "10115";
        hotel.Amenities = "wifi,breakfast,bar,fitness,luggageStorage,petsAllowed,parking";

        hotel.PublicBookingSettings = new PublicBookingSettings
        {
            IsEnabled = true,
            MinNights = 1,
            MaxNights = 21,
            MaxAdvanceDays = 365,
            MinAdvanceHours = 0,
            MaxAdults = 4,
            MaxChildren = 3,
            ConfirmationMode = PublicBookingConfirmationMode.Instant
        };

        hotel.CancellationPolicy = new CancellationPolicy
        {
            Type = CancellationPolicyType.Flexible,
            FreeCancellationDaysBeforeArrival = 3,
            CutoffLocalTime = new TimeOnly(18, 0),
            LateCancellationFeePercent = 90.00m,
            NoShowFeePercent = 90.00m
        };

        hotel.LegalProfile = new HotelLegalProfile
        {
            LegalEntityName = "HotelCore Berlin Betriebs GmbH",
            LegalForm = "GmbH",
            RepresentedBy = "Anna Becker (Geschäftsführerin)",
            AddressLine = "Chausseestraße 5",
            PostalCode = "10115",
            City = "Berlin",
            Country = Country.DE,
            Phone = "+49 30 1234567",
            Email = "info@hotelcore.local",
            RegisterCourt = "Amtsgericht Berlin-Charlottenburg",
            RegisterNumber = "HRB 284913 B",
            SupervisoryAuthority = null,
            ParticipatesInDisputeResolution = false,
            OnlineDisputeResolutionUrl = "https://ec.europa.eu/consumers/odr/",
            DisputeResolutionNotice =
                "Wir sind nicht bereit und nicht verpflichtet, an Streitbeilegungsverfahren vor " +
                "einer Verbraucherschlichtungsstelle teilzunehmen (§ 36 VSBG)."
        };

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    // -------------------------------------------------------------------------------------------
    // Hukuki belgeler
    // -------------------------------------------------------------------------------------------

    private static async Task SeedLegalDocumentsAsync(
        AppDbContext context,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        var existing = await context.HotelLegalDocuments
            .IgnoreQueryFilters()
            .Where(document => document.HotelId == hotelId)
            .Select(document => new { document.Key, document.Culture, document.Version })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = LegalDocuments
            .Where(seed => !existing.Any(row =>
                string.Equals(row.Key, seed.Key, StringComparison.Ordinal)
                && string.Equals(row.Culture, seed.Culture, StringComparison.Ordinal)
                && string.Equals(row.Version, LegalVersion, StringComparison.Ordinal)))
            .Select(seed => new HotelLegalDocument
            {
                HotelId = hotelId,
                Key = seed.Key,
                Culture = seed.Culture,
                Version = LegalVersion,
                Title = seed.Title,
                BodyHtml = seed.BodyHtml,
                IsActive = true,
                // Npgsql "timestamp with time zone" kolonuna yalnizca offset 0 yazar; deger
                // 2026-07-01 00:00 Berlin yerel saatinin (CEST, +02:00) UTC karsiligidir.
                PublishedAt = new DateTimeOffset(2026, 6, 30, 22, 0, 0, TimeSpan.Zero)
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.HotelLegalDocuments.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Demo hukuki metinler. <b>Hukuki danışmanlık değildir</b> — yapı ve zorunlu başlıklar
    /// gerçektir (§5 DDG, DSGVO Art. 13, §312g Abs. 2 Nr. 9 BGB), içerik kurgusaldır.
    /// Gövde yalnızca izinli etiketleri (<c>p, h2, ul, li, strong</c>) kullanır: sanitizasyon
    /// sunucunun sorumluluğudur ve seed o kuralı ihlal eden bir örnek vermez.
    /// </summary>
    private static readonly LegalDocumentSeed[] LegalDocuments =
    [
        new("terms", "de", "Allgemeine Geschäftsbedingungen",
            """
            <h2>1. Geltungsbereich</h2>
            <p>Diese Allgemeinen Geschäftsbedingungen gelten für Beherbergungsverträge zwischen der
            HotelCore Berlin Betriebs GmbH und ihren Gästen sowie für alle damit verbundenen
            Leistungen.</p>
            <h2>2. Vertragsschluss</h2>
            <p>Der Vertrag kommt mit der Bestätigung der Buchung durch das Hotel zustande. Die
            Bestätigung wird unmittelbar nach Abschluss der Online-Buchung per E-Mail versendet.</p>
            <h2>3. Preise und Zahlung</h2>
            <p>Alle Preise verstehen sich einschließlich der gesetzlichen Mehrwertsteuer. Die
            Berliner Übernachtungsteuer wird gesondert ausgewiesen und ist nur zu entrichten, wenn
            der Aufenthalt tatsächlich stattfindet. Die Zahlung erfolgt bei Anreise im Hotel.</p>
            <h2>4. An- und Abreise</h2>
            <ul>
              <li>Die Zimmer stehen ab 15:00 Uhr zur Verfügung.</li>
              <li>Am Abreisetag sind die Zimmer bis 11:00 Uhr zu räumen.</li>
            </ul>
            <h2>5. Rücktritt des Gastes</h2>
            <p>Eine kostenfreie Stornierung ist bis 18:00 Uhr Ortszeit drei Tage vor dem Anreisetag
            möglich. Danach berechnet das Hotel <strong>90 % des Übernachtungspreises</strong>. Die
            Übernachtungsteuer entfällt in diesem Fall vollständig.</p>
            <h2>6. Haustiere</h2>
            <p>Haustiere sind nach vorheriger Absprache gegen ein Entgelt von 20,00 € pro Nacht
            gestattet.</p>
            """),
        new("terms", "en", "Terms and Conditions",
            """
            <h2>1. Scope</h2>
            <p>These terms and conditions apply to accommodation contracts between HotelCore Berlin
            Betriebs GmbH and its guests, and to all related services.</p>
            <h2>2. Conclusion of contract</h2>
            <p>The contract is concluded when the hotel confirms the booking. The confirmation is
            sent by e-mail immediately after the online booking is completed.</p>
            <h2>3. Prices and payment</h2>
            <p>All prices include statutory VAT. The Berlin city tax is shown separately and is only
            payable if the stay actually takes place. Payment is due on arrival at the hotel.</p>
            <h2>4. Arrival and departure</h2>
            <ul>
              <li>Rooms are available from 15:00.</li>
              <li>Rooms must be vacated by 11:00 on the day of departure.</li>
            </ul>
            <h2>5. Cancellation by the guest</h2>
            <p>Free cancellation is possible until 18:00 local time three days before arrival. After
            that the hotel charges <strong>90 % of the accommodation price</strong>. The city tax is
            not charged in that case.</p>
            """),
        new("privacy", "de", "Datenschutzerklärung",
            """
            <h2>1. Verantwortlicher</h2>
            <p>HotelCore Berlin Betriebs GmbH, Chausseestraße 5, 10115 Berlin, vertreten durch Anna
            Becker.</p>
            <h2>2. Zwecke und Rechtsgrundlagen</h2>
            <p>Wir verarbeiten Ihre Buchungsdaten (Vor- und Nachname, E-Mail-Adresse, optional
            Telefonnummer) zur Begründung und Durchführung des Beherbergungsvertrags gemäß
            <strong>Art. 6 Abs. 1 lit. b DSGVO</strong>.</p>
            <h2>3. Datenminimierung</h2>
            <p>Bei der Buchung erheben wir <strong>keine</strong> Geburtsdaten, Staatsangehörigkeit,
            Ausweisnummern oder vollständigen Wohnanschriften. Diese Angaben sind Bestandteil des
            Meldescheins nach §§ 29, 30 BMG und werden erst bei der Anreise erhoben.</p>
            <h2>4. Zahlungsdaten</h2>
            <p>Wir erheben und speichern <strong>keine Kartendaten</strong>. Die Zahlung erfolgt vor
            Ort im Hotel.</p>
            <h2>5. Speicherdauer</h2>
            <p>Buchungs- und Rechnungsdaten werden gemäß den handels- und steuerrechtlichen
            Aufbewahrungsfristen (§ 147 AO) zehn Jahre aufbewahrt.</p>
            <h2>6. Ihre Rechte</h2>
            <ul>
              <li>Auskunft (Art. 15 DSGVO)</li>
              <li>Berichtigung (Art. 16 DSGVO)</li>
              <li>Löschung (Art. 17 DSGVO), soweit keine Aufbewahrungspflicht entgegensteht</li>
              <li>Beschwerde bei der Berliner Beauftragten für Datenschutz und Informationsfreiheit</li>
            </ul>
            <h2>7. Speicherung auf Ihrem Endgerät</h2>
            <p>Wir verwenden ausschließlich unbedingt erforderliche Speicherung (§ 25 Abs. 2 Nr. 2
            TDDDG): die Kennung Ihrer aktuellen Reservierungsvormerkung und Ihre Sprachauswahl.
            Analyse- oder Marketingdienste setzen wir nicht ein.</p>
            """),
        new("privacy", "en", "Privacy Policy",
            """
            <h2>1. Controller</h2>
            <p>HotelCore Berlin Betriebs GmbH, Chausseestrasse 5, 10115 Berlin, represented by Anna
            Becker.</p>
            <h2>2. Purposes and legal bases</h2>
            <p>We process your booking data (first and last name, e-mail address, optionally phone
            number) in order to conclude and perform the accommodation contract pursuant to
            <strong>Art. 6(1)(b) GDPR</strong>.</p>
            <h2>3. Data minimisation</h2>
            <p>We do <strong>not</strong> collect date of birth, nationality, identity document
            numbers or full home addresses at booking time. Those are part of the registration form
            under §§ 29, 30 BMG and are collected on arrival.</p>
            <h2>4. Payment data</h2>
            <p>We do <strong>not</strong> collect or store card data. Payment is made at the
            property.</p>
            <h2>5. Retention</h2>
            <p>Booking and invoice data are retained for ten years under German commercial and tax
            law (§ 147 AO).</p>
            """),
        new("withdrawal", "de", "Hinweis zum Widerrufsrecht",
            """
            <h2>Kein Widerrufsrecht bei Beherbergung zu einem bestimmten Termin</h2>
            <p>Bei Verträgen über die Beherbergung zu einem bestimmten Termin besteht
            <strong>kein gesetzliches Widerrufsrecht</strong>. Rechtsgrundlage ist
            <strong>§ 312g Abs. 2 Nr. 9 BGB</strong>.</p>
            <p>Davon unberührt bleibt Ihr <strong>vertragliches Stornorecht</strong> nach Ziffer 5
            unserer Allgemeinen Geschäftsbedingungen: Sie können Ihre Buchung bis 18:00 Uhr Ortszeit
            drei Tage vor Anreise kostenfrei stornieren.</p>
            """),
        new("withdrawal", "en", "Notice on the right of withdrawal",
            """
            <h2>No right of withdrawal for accommodation on a specific date</h2>
            <p>For contracts covering accommodation on a specific date there is
            <strong>no statutory right of withdrawal</strong>. The legal basis is
            <strong>section 312g(2) no. 9 of the German Civil Code (BGB)</strong>.</p>
            <p>This does not affect your <strong>contractual cancellation right</strong> under
            clause 5 of our terms and conditions: you may cancel free of charge until 18:00 local
            time three days before arrival.</p>
            """)
    ];

    // -------------------------------------------------------------------------------------------
    // Görseller
    // -------------------------------------------------------------------------------------------

    private static readonly ImageSeed[] HotelImageSeeds =
    [
        new($"{ImageRoot}/hotel-fassade.svg", 0, "Fassade des HotelCore Berlin Mitte an der Chausseestraße", 1600, 900),
        new($"{ImageRoot}/hotel-lobby.svg", 1, "Lobby mit Empfang und Sitzbereich", 1600, 900),
        new($"{ImageRoot}/hotel-fruehstueck.svg", 2, "Frühstücksraum mit Buffet", 1600, 900)
    ];

    private static async Task SeedHotelImagesAsync(
        AppDbContext context,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        var existing = await context.HotelImages
            .IgnoreQueryFilters()
            .Where(image => image.HotelId == hotelId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Demo kümesinden ÇIKARILMIŞ satırlar temizlenir (yalnızca demo kökü altındakiler;
        // geliştiricinin/müşterinin eklediği görseller korunur). Gerekçe: seed dosya adları
        // değiştiğinde (örneğin .jpg → .svg) yalnızca "eksikleri ekle" mantığı eski satırları
        // bırakır ve galeri sessizce ikiye katlanır.
        var stale = existing
            .Where(image => image.Url.StartsWith(ImageRoot, StringComparison.Ordinal)
                            && !HotelImageSeeds.Any(seed =>
                                string.Equals(seed.Url, image.Url, StringComparison.Ordinal)))
            .ToList();

        var existingUrls = existing.Select(image => image.Url).ToList();

        var missing = HotelImageSeeds
            .Where(seed => !existingUrls.Contains(seed.Url, StringComparer.Ordinal))
            .Select(seed => new HotelImage
            {
                HotelId = hotelId,
                Url = seed.Url,
                SortOrder = seed.SortOrder,
                AltText = seed.AltText,
                Width = seed.Width,
                Height = seed.Height
            })
            .ToList();

        if (stale.Count == 0 && missing.Count == 0)
        {
            return;
        }

        context.HotelImages.RemoveRange(stale);
        context.HotelImages.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------------------------
    // Oda tipi içeriği: Almanca metin + görsel + çeviri
    // -------------------------------------------------------------------------------------------

    private static readonly RoomTypeContentSeed[] RoomTypeContents =
    [
        new(
            "SGL",
            "Ruhiges Einzelzimmer zum begrünten Innenhof, mit Schreibtisch, Regendusche und "
            + "kostenfreiem WLAN. Ideal für Geschäftsreisende.",
            "wifi,desk,safe,airConditioning",
            [new($"{ImageRoot}/sgl-1.svg", 0, "Einzelzimmer mit Schreibtisch und Fenster zum Innenhof", 1600, 900),
             new($"{ImageRoot}/sgl-2.svg", 1, "Badezimmer des Einzelzimmers mit Regendusche", 1600, 900)],
            new TranslatedText("Single Room", "Quiet single room facing the courtyard, with desk, rain shower and free Wi-Fi. Ideal for business travellers."),
            new TranslatedText("Tek Kişilik Oda", "İç avluya bakan sessiz tek kişilik oda; çalışma masası, yağmur duşu ve ücretsiz Wi-Fi. İş seyahatleri için ideal.")),
        new(
            "DBL",
            "Großzügiges Doppelzimmer mit Kingsize-Bett, Sitzecke und bodentiefen Fenstern zur "
            + "Chausseestraße. Nespresso-Maschine und Minibar inklusive.",
            "wifi,minibar,safe,coffeeMachine,airConditioning",
            [new($"{ImageRoot}/dbl-1.svg", 0, "Doppelzimmer mit Kingsize-Bett und Sitzecke", 1600, 900),
             new($"{ImageRoot}/dbl-2.svg", 1, "Blick aus dem Doppelzimmer auf die Chausseestraße", 1600, 900)],
            new TranslatedText("Double Room", "Spacious double room with king-size bed, seating area and floor-to-ceiling windows facing Chausseestrasse. Nespresso machine and minibar included."),
            new TranslatedText("Çift Kişilik Oda", "King-size yatak, oturma köşesi ve Chausseestrasse'ye bakan tavandan tabana pencereli geniş çift kişilik oda. Nespresso makinesi ve minibar dahil.")),
        new(
            "SUI",
            "Suite mit separatem Wohnbereich, Balkon und Blick über Berlin-Mitte. Für bis zu vier "
            + "Personen, mit freistehender Badewanne und Regendusche.",
            "wifi,minibar,balcony,safe,bathtub,coffeeMachine",
            [new($"{ImageRoot}/sui-1.svg", 0, "Wohnbereich der Suite mit Sofa und Esstisch", 1600, 900),
             new($"{ImageRoot}/sui-2.svg", 1, "Schlafbereich der Suite", 1600, 900),
             new($"{ImageRoot}/sui-3.svg", 2, "Balkon der Suite mit Blick über Berlin-Mitte", 1600, 900)],
            new TranslatedText("Suite", "Suite with a separate living area, balcony and views over Berlin-Mitte. For up to four guests, with a freestanding bathtub and rain shower."),
            new TranslatedText("Suit", "Ayrı oturma alanı, balkon ve Berlin-Mitte manzaralı suit. Dört kişiye kadar; ayaklı küvet ve yağmur duşu."))
    ];

    private static async Task SeedRoomTypeContentAsync(
        AppDbContext context,
        Guid hotelId,
        bool applyGermanTexts,
        CancellationToken cancellationToken)
    {
        var roomTypes = await context.RoomTypes
            .IgnoreQueryFilters()
            .Where(roomType => roomType.HotelId == hotelId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (roomTypes.Count == 0)
        {
            return;
        }

        var existingImages = await context.RoomTypeImages
            .IgnoreQueryFilters()
            .Where(image => image.HotelId == hotelId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Otel görselleriyle aynı gerekçe: demo kümesinden çıkarılmış satırlar temizlenir,
        // demo kökü dışındaki (elle eklenmiş) görsellere dokunulmaz.
        var seededUrls = RoomTypeContents
            .SelectMany(content => content.Images)
            .Select(image => image.Url)
            .ToList();

        var staleImages = existingImages
            .Where(image => image.Url.StartsWith(ImageRoot, StringComparison.Ordinal)
                            && !seededUrls.Contains(image.Url, StringComparer.Ordinal))
            .ToList();

        if (staleImages.Count > 0)
        {
            context.RoomTypeImages.RemoveRange(staleImages);
        }

        var existingImageUrls = existingImages.Select(image => image.Url).ToList();

        var newImages = new List<RoomTypeImage>();
        var newTranslations = new List<Translation>();

        foreach (var content in RoomTypeContents)
        {
            var roomType = roomTypes.Find(candidate =>
                string.Equals(candidate.Code, content.Code, StringComparison.Ordinal));

            if (roomType is null)
            {
                continue;
            }

            // Almanca metinler yalnızca kanal İLK KEZ açılırken yazılır: sonraki çalıştırmalarda
            // geliştiricinin/müşterinin düzenlemesi ezilmez.
            if (applyGermanTexts)
            {
                roomType.Description = content.GermanDescription;
                roomType.Amenities = content.Amenities;
            }

            newImages.AddRange(content.Images
                .Where(image => !existingImageUrls.Contains(image.Url, StringComparer.Ordinal))
                .Select(image => new RoomTypeImage
                {
                    HotelId = hotelId,
                    RoomTypeId = roomType.Id,
                    Url = image.Url,
                    SortOrder = image.SortOrder,
                    AltText = image.AltText,
                    Width = image.Width,
                    Height = image.Height
                }));

            newTranslations.AddRange(BuildTranslations(roomType.Id, "en", content.English));
            newTranslations.AddRange(BuildTranslations(roomType.Id, "tr", content.Turkish));
        }

        await AddMissingTranslationsAsync(context, newTranslations, cancellationToken).ConfigureAwait(false);

        if (newImages.Count > 0)
        {
            context.RoomTypeImages.AddRange(newImages);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<Translation> BuildTranslations(
        Guid roomTypeId,
        string culture,
        TranslatedText text)
    {
        yield return new Translation
        {
            EntityType = nameof(RoomType),
            EntityId = roomTypeId,
            Field = nameof(RoomType.Name),
            Culture = culture,
            Text = text.Name
        };

        yield return new Translation
        {
            EntityType = nameof(RoomType),
            EntityId = roomTypeId,
            Field = nameof(RoomType.Description),
            Culture = culture,
            Text = text.Description
        };
    }

    private static async Task AddMissingTranslationsAsync(
        AppDbContext context,
        List<Translation> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        var entityIds = candidates.Select(translation => translation.EntityId).Distinct().ToList();

        var existing = await context.Translations
            .Where(translation => translation.EntityType == nameof(RoomType)
                                  && entityIds.Contains(translation.EntityId))
            .Select(translation => new { translation.EntityId, translation.Field, translation.Culture })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = candidates
            .Where(candidate => !existing.Any(row =>
                row.EntityId == candidate.EntityId
                && string.Equals(row.Field, candidate.Field, StringComparison.Ordinal)
                && string.Equals(row.Culture, candidate.Culture, StringComparison.Ordinal)))
            .ToList();

        if (missing.Count > 0)
        {
            context.Translations.AddRange(missing);
        }
    }

    // -------------------------------------------------------------------------------------------
    // Web'e uygulanabilir fiyat planı
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>Neden bu plan zorunlu:</b> fiyat seçimi kanalı birebir karşılaştırır, yani
    /// <c>Channel = Direct</c> planları <c>Website</c> rezervasyonlarına uygulanmaz
    /// (architecture-public-booking.md §7.1). Kanal başına plan yoksa web fiyatı sessizce
    /// <c>RoomType.BasePrice</c>'a düşer ve demo, sezon fiyatlandırmasını hiç göstermez.
    /// Bu yüzden seed <c>Channel = null</c> ("tüm kanallar") bir plan içerir.
    /// <para>
    /// Aralık <b>takvim yılıdır</b> ve plan adı yılı taşır: böylece seeder yıllar boyunca tekrar
    /// çalıştırıldığında üretilen planlar <c>EX_RatePlans_NoOverlappingActivePlans</c> kısıtıyla
    /// çakışmaz.
    /// </para>
    /// </summary>
    private static async Task SeedWebRatePlansAsync(
        AppDbContext context,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        var roomTypes = await context.RoomTypes
            .IgnoreQueryFilters()
            .Where(roomType => roomType.HotelId == hotelId)
            .Select(roomType => new { roomType.Id, roomType.BasePrice })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (roomTypes.Count == 0)
        {
            return;
        }

        // Yalnızca "tüm kanallar" (Channel = null) ve AKTİF planlar çakışma üretebilir —
        // EX_RatePlans_NoOverlappingActivePlans kısıtının anahtarı budur. Ada göre kontrol
        // YETMEZ: geliştiricinin elle eklediği, başka adlı ama aynı aralığı kaplayan bir plan
        // seed'i patlatırdı (idempotent bir seeder mevcut veriyi kabul etmelidir).
        var existingRanges = await context.RatePlans
            .IgnoreQueryFilters()
            .Where(plan => plan.HotelId == hotelId && plan.Channel == null && plan.IsActive)
            .Select(plan => new { plan.RoomTypeId, plan.ValidFrom, plan.ValidTo })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var currentYear = DateTime.UtcNow.Year;
        var plans = new List<RatePlan>();

        foreach (var year in new[] { currentYear, currentYear + 1 })
        {
            var validFrom = new DateOnly(year, 1, 1);
            var validTo = new DateOnly(year, 12, 31);

            plans.AddRange(roomTypes
                // Kısıtla AYNI kesişim testi: kapalı aralık [ValidFrom, ValidTo].
                .Where(roomType => !existingRanges.Any(row =>
                    row.RoomTypeId == roomType.Id && row.ValidFrom <= validTo && validFrom <= row.ValidTo))
                .Select(roomType => new RatePlan
                {
                    HotelId = hotelId,
                    RoomTypeId = roomType.Id,
                    Name = $"Flex-Rate {year} (alle Kanäle)",
                    // Liste fiyatının biraz üstünde: demo, plan fiyatının BasePrice'ı gerçekten
                    // geçersiz kıldığını görünür kılsın.
                    Price = decimal.Round(roomType.BasePrice * 1.08m, 2),
                    ValidFrom = validFrom,
                    ValidTo = validTo,
                    Channel = null,
                    IsActive = true
                }));
        }

        if (plans.Count == 0)
        {
            return;
        }

        context.RatePlans.AddRange(plans);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record LegalDocumentSeed(string Key, string Culture, string Title, string BodyHtml);

    private sealed record ImageSeed(string Url, int SortOrder, string AltText, int Width, int Height);

    private sealed record TranslatedText(string Name, string Description);

    private sealed record RoomTypeContentSeed(
        string Code,
        string GermanDescription,
        string Amenities,
        IReadOnlyList<ImageSeed> Images,
        TranslatedText English,
        TranslatedText Turkish);
}
