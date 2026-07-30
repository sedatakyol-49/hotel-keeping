using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelCore.Infrastructure.Persistence.Migrations
{
    // Rezervasyon + Faturalama modülleri yazılırken tespit edilen domain boşluklarını kapatır:
    //
    // 1) Invoices.CancelsInvoiceId — storno çiftinin GERİ referansı (self FK + index + CHECK).
    //    İleri yön (CancelledByInvoiceId) zaten vardı; ters yön saklanmadığı için "bu belge neyi
    //    iptal ediyor?" sorusu her satır için ilintili alt sorgu gerektiriyordu.
    // 2) Hotels.TaxProfile_CityTaxExemptChildren / _CityTaxChildAgeLimit — Kurtaxe çocuk muafiyeti.
    //    Bool varsayılanı FALSE, yaş sınırı NULL: mevcut otellerin Kurtaxe hesabı
    //    ((yetişkin + çocuk) × gece) DEĞİŞMEZ, muafiyet opt-in'dir.
    // 3) RatePlans çakışma kısıtı — EXCLUDE USING gist + daterange (aşağıda ham SQL).
    //
    // (InvoiceAuditAction'a eklenen Updated/PaymentRecorded değerleri şema değişikliği
    //  GEREKTİRMEZ: enum string olarak saklanır, mevcut satırlar etkilenmez.)
    //
    // ---- Down davranışı -------------------------------------------------------------------
    // * Yeni kolonlar DROP edilir → içlerindeki veri (storno geri referansları, muafiyet
    //   ayarları) GERİ DÖNÜŞSÜZ kaybolur. Kayıp veri "türetilebilir" olduğu için kabul edilebilir:
    //   geri referans CancelledByInvoiceId'den, muafiyet ayarı ise otel yapılandırmasından yeniden
    //   kurulabilir. Yine de Down, üretimde veri kaybı anlamına gelir.
    // * EXCLUDE kısıtı ve CHECK'ler DROP edilir; bu işlem her zaman güvenlidir (kısıt kaldırmak
    //   mevcut satırları doğrulamaz).
    // * btree_gist extension'ı BİLİNÇLİ olarak DROP EDİLMEZ: extension veritabanı geneli bir
    //   nesnedir, başka şemalar/kısıtlar ona bağlı olabilir ve düşürülmesi Down'ı geri
    //   döndürülemez biçimde kırabilir. Boşta duran bir extension'ın maliyeti yoktur.
    /// <inheritdoc />
    public partial class CloseDomainGapsForInvoicingAndRates : Migration
    {
        /// <summary>Fiyat planı çakışma kısıtının adı (EX_ öneki: EXCLUDE kısıtı).</summary>
        private const string RatePlanOverlapConstraint = "EX_RatePlans_NoOverlappingActivePlans";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CancelsInvoiceId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxProfile_CityTaxChildAgeLimit",
                table: "Hotels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TaxProfile_CityTaxExemptChildren",
                table: "Hotels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RatePlans_ValidRange",
                table: "RatePlans",
                sql: "\"ValidFrom\" <= \"ValidTo\"");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CancelsInvoiceId",
                table: "Invoices",
                column: "CancelsInvoiceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_NoSelfCancellation",
                table: "Invoices",
                sql: "(\"CancelledByInvoiceId\" IS NULL OR \"CancelledByInvoiceId\" <> \"Id\") AND (\"CancelsInvoiceId\" IS NULL OR \"CancelsInvoiceId\" <> \"Id\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hotels_CityTaxChildAgeLimit",
                table: "Hotels",
                sql: "\"TaxProfile_CityTaxChildAgeLimit\" IS NULL OR (\"TaxProfile_CityTaxChildAgeLimit\" >= 0 AND \"TaxProfile_CityTaxChildAgeLimit\" <= 99)");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Invoices_CancelsInvoiceId",
                table: "Invoices",
                column: "CancelsInvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ================= Fiyat planı çakışma kısıtı (veritabanı düzeyinde) =================
            //
            // NEDEN: tek koruma handler'ın ön kontrolüydü (SELECT ... ANY). İki eşzamanlı istek
            // birbirinin henüz commit edilmemiş satırını görmediği için ikisi de ön kontrolü geçer
            // ve aynı oda tipi + kanal için ÇAKIŞAN iki plan yazılabilir; o gece için hangi fiyatın
            // geçerli olduğu belirsizleşir. Bunu kesin olarak yalnızca veritabanı engelleyebilir.
            //
            // NEDEN "EXCLUDE USING gist": çakışma bir EŞİTLİK değil ARALIK KESİŞİMİ kuralıdır;
            // unique index bunu ifade edemez. PostgreSQL'in aralık dışlama kısıtı tam olarak bu iş
            // içindir ve INSERT/UPDATE anında (yarış durumları dâhil) atomik olarak uygular.
            //
            // btree_gist: gist erişim yöntemi uuid/text üzerinde "=" operatörünü ancak bu extension
            // ile destekler. Extension'ı oluşturmak superuser (veya CREATE yetkisi) ister; kısıtlı
            // yetkili ortamlarda DBA'nın extension'ı önceden kurması gerekir.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // Kısıt anahtarının parçaları:
            //   "RoomTypeId" WITH =        -> çakışma yalnızca AYNI oda tipi içinde aranır.
            //                                 HotelId BİLİNÇLİ olarak anahtarda YOK: oda tipi zaten
            //                                 tek bir otele aittir, HotelId eklemek kısıtı yalnızca
            //                                 ZAYIFLATIRDI (HotelId'si yanlış yazılmış iki satır
            //                                 çakışmıyor sayılırdı).
            //   COALESCE("Channel", '*')   -> kısıt KANAL BAZINDA çalışır. Sözleşme gereği
            //                                 "kanal bazlı çakışma saymaz": aynı aralıkta
            //                                 Channel = NULL (tüm kanallar) ve
            //                                 Channel = 'BookingCom' planları birlikte var olabilir
            //                                 (fiyat seçiminde kanala özel plan önce gelir).
            //                                 SENTINEL ŞART: EXCLUDE kısıtında NULL, "=" ile hiçbir
            //                                 değere -NULL'a da- eşit sayılmaz; yani ham "Channel"
            //                                 kullanılırsa iki NULL plan çakışsa bile YAKALANMAZ.
            //                                 COALESCE ile NULL'lar birbirine eşitlenir, gerçek
            //                                 kanallardan ayrı kalır. '*' güvenli bir sentinel'dir:
            //                                 kolonda ReservationChannel enum ADLARI saklanır
            //                                 (Direct, BookingCom, ...), '*' asla oluşamaz.
            //   daterange(..., '[]')       -> KAPALI aralık: uç noktada eşitlik ÇAKIŞMADIR
            //                                 (handler'ın [ValidFrom, ValidTo] semantiği ile aynı;
            //                                 rezervasyonun yarı açık gece aralığından farklı).
            //   WHERE ("IsActive")         -> kısmi kısıt: pasif planlar çakışma üretmez
            //                                 (handler ön kontrolü de böyle davranır).
            //
            // İhlal SQLSTATE 23P01 (exclusion_violation) üretir; AppDbContext bunu 409 Conflict'e
            // çevirir (bkz. FindConflictingViolation) — kullanıcı 500 görmez.
            migrationBuilder.Sql($"""
                ALTER TABLE "RatePlans"
                    ADD CONSTRAINT "{RatePlanOverlapConstraint}"
                    EXCLUDE USING gist (
                        "RoomTypeId" WITH =,
                        (COALESCE("Channel"::text, '*')) WITH =,
                        daterange("ValidFrom", "ValidTo", '[]') WITH &&
                    ) WHERE ("IsActive");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Kısıt önce düşürülür: sonraki DROP'lar (özellikle CHECK ve kolonlar) ondan bağımsızdır,
            // ama sıra netlik için Up'ın tersidir. btree_gist extension'ı KALIR (yukarıdaki not).
            migrationBuilder.Sql($"""ALTER TABLE "RatePlans" DROP CONSTRAINT IF EXISTS "{RatePlanOverlapConstraint}";""");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Invoices_CancelsInvoiceId",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RatePlans_ValidRange",
                table: "RatePlans");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CancelsInvoiceId",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_NoSelfCancellation",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Hotels_CityTaxChildAgeLimit",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "CancelsInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TaxProfile_CityTaxChildAgeLimit",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "TaxProfile_CityTaxExemptChildren",
                table: "Hotels");
        }
    }
}
