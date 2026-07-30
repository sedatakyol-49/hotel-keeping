using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Hr.Common;

/// <summary>
/// İK modüllerinin (izin / Zeiterfassung / vardiya) paylaştığı çalışan araması.
/// <para>
/// Üç slice de aynı soruyu sorar: "bu çalışan aktif otelde var mı?" — cevap tek yerde
/// üretilir ki 404/tenant semantiği modüller arasında farklılaşmasın.
/// </para>
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir;
/// burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ. Tek istisna
/// <see cref="GetInHotelAsync"/> içindeki açık otel karşılaştırmasıdır: Head Office
/// kullanıcısında filtre bypass edildiği için (<c>CanAccessAllHotels</c>) başka otelin
/// çalışanı da görünür olur; alt kayıtların ebeveyniyle aynı otelde kalması bu yüzden
/// ayrıca doğrulanır.
/// </para>
/// </summary>
internal sealed class EmployeeLookup(IAppDbContext database)
{
    /// <summary>
    /// Aktif oteldeki çalışanı (takip edilen hâliyle) döndürür. Kayıt yoksa <b>veya başka
    /// otele aitse</b> 404 — çalışanın varlığı sızdırılmaz ve alt kayıt yanlış otele bağlanmaz.
    /// </summary>
    public async Task<Employee> GetInHotelAsync(
        Guid employeeId,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        var employee = await database.Employees
            .FirstOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);

        if (employee is null || employee.HotelId != hotelId)
        {
            throw new NotFoundException("Calisan bulunamadi.");
        }

        return employee;
    }

    /// <summary>
    /// Mevcut bir alt kaydın (izin talebi vb.) çalışanını döndürür. Otel kontrolü yapılmaz:
    /// ebeveyn kayıt zaten tenant filtresinden geçmiştir, çalışan da onunla aynı oteldedir.
    /// Çalışan soft-delete edilmişse 404.
    /// </summary>
    public async Task<Employee> GetTrackedAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await database.Employees
            .FirstOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);

        return employee ?? throw new NotFoundException("Calisan bulunamadi.");
    }
}
