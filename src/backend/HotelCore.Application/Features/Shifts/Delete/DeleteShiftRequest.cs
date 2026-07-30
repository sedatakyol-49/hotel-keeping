using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Shifts.Delete;

/// <summary>
/// <c>DELETE /api/v1/shifts/{id}</c> — planlanan vardiyanın kaldırılması.
/// <para>
/// <c>Shift</c> soft-delete edilebilir değildir (bilinçli): plan geleceğe yöneliktir, silinen
/// bir plan satırının saklanması bir yükümlülük değildir. Gerçekleşen mesai <c>TimeEntry</c>'de
/// tutulur ve bu işlemden etkilenmez.
/// </para>
/// </summary>
public sealed record DeleteShiftRequest(Guid Id) : IRequest<Unit>;
