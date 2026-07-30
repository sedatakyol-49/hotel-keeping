using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelCore.Infrastructure.Persistence.Migrations
{
    // ISoftDeletable entity'lerdeki unique index'ler kısmi (partial) index'e dönüştürülür:
    // benzersizlik yalnızca canlı (NOT "IsDeleted") satırlar arasında aranır.
    //
    // NEDEN: global query filter soft-deleted satırı gizlediği için handler'ın çakışma ön kontrolü
    // silinmiş kaydı görmüyordu; filtresiz index yüzünden INSERT 23505 ile patlıyor ve kullanıcıya
    // 409 yerine 500 dönüyordu. Ayrıca silinen bir kaydın doğal anahtarı (oda numarası, personel
    // numarası, e-posta) bir daha asla kullanılamıyordu.
    //
    // Invoice(HotelId, InvoiceNumber) BİLİNÇLİ olarak kapsam dışıdır: GoBD gereği fatura numarası
    // saklama süresi boyunca (silinmiş görünen satırlar dâhil) benzersiz kalmalıdır.
    /// <inheritdoc />
    public partial class FilterUniqueIndexesOnSoftDelete : Migration
    {
        // Up güvenlidir: filtreli index mevcut index'in alt kümesini kapsar, dolayısıyla
        // veritabanında hâlihazırda soft-deleted (hatta çift) satırlar olsa bile çakışma olmaz.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_RoomTypes_HotelId_Code",
                table: "RoomTypes");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_HotelId_Number",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_HotelId_ReservationNumber",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_HeadOfficeId_Name",
                table: "Hotels");

            migrationBuilder.DropIndex(
                name: "IX_Employees_HotelId_StaffNumber",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypes_HotelId_Code",
                table: "RoomTypes",
                columns: new[] { "HotelId", "Code" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_HotelId_Number",
                table: "Rooms",
                columns: new[] { "HotelId", "Number" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_HotelId_ReservationNumber",
                table: "Reservations",
                columns: new[] { "HotelId", "ReservationNumber" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_HeadOfficeId_Name",
                table: "Hotels",
                columns: new[] { "HeadOfficeId", "Name" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_HotelId_StaffNumber",
                table: "Employees",
                columns: new[] { "HotelId", "StaffNumber" },
                unique: true,
                filter: "NOT \"IsDeleted\"");
        }

        // DİKKAT — Down tek yönlü güvenli DEĞİLDİR: filtresiz unique index'i geri kurmak, kısmi
        // index yürürlükteyken üretilmiş olan "silinmiş + canlı aynı doğal anahtar" çiftlerinde
        // (örn. soft-deleted oda 10 + yeni oda 10) PostgreSQL'de 23505 ile BAŞARISIZ olur.
        // Bu kasıtlıdır: sessizce veri kaybetmek yerine geri alma durur. Geri almak gerçekten
        // gerekiyorsa önce çakışan soft-deleted satırlar arşivlenip fiziksel silinmelidir
        // (faturalarla ilişkili satırlar için GoBD saklama kuralı gözetilerek).
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_RoomTypes_HotelId_Code",
                table: "RoomTypes");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_HotelId_Number",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_HotelId_ReservationNumber",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_HeadOfficeId_Name",
                table: "Hotels");

            migrationBuilder.DropIndex(
                name: "IX_Employees_HotelId_StaffNumber",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypes_HotelId_Code",
                table: "RoomTypes",
                columns: new[] { "HotelId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_HotelId_Number",
                table: "Rooms",
                columns: new[] { "HotelId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_HotelId_ReservationNumber",
                table: "Reservations",
                columns: new[] { "HotelId", "ReservationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_HeadOfficeId_Name",
                table: "Hotels",
                columns: new[] { "HeadOfficeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_HotelId_StaffNumber",
                table: "Employees",
                columns: new[] { "HotelId", "StaffNumber" },
                unique: true);
        }
    }
}
