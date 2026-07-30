using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Shifts.Common;

/// <summary>Create ve Update isteklerinin paylaştığı gövde sözleşmesi.</summary>
// CA1716 bastırılır: "Date" bazı .NET dillerinde (VB) ayrılmış sözcüktür, ancak bu arayüz
// sözleşmedeki JSON alan adını (date) yansıtır ve yalnızca C# içinde uygulanır. Adı değiştirmek
// gövdeyi/DTO'yu sözleşmeden uzaklaştırırdı; Shift entity'sindeki alan adı da "Date"dir.
#pragma warning disable CA1716
public interface IShiftWriteRequest
{
    Guid EmployeeId { get; }

    DateOnly Date { get; }

    ShiftType ShiftType { get; }

    string? Note { get; }
}
#pragma warning restore CA1716
