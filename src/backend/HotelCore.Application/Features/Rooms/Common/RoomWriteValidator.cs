using FluentValidation;

namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// Oda yazma isteklerinin ortak doğrulaması (api-contracts.md → "Doğrulama kuralları"):
/// <c>number</c> 1–10 karakter, <c>floor</c> −5…99. Oda numarasının otel içindeki
/// <b>benzersizliği</b> handler'da kontrol edilir (çakışma 409), oda tipinin varlığı da (404).
/// </summary>
/// <typeparam name="TRequest">Create veya Update isteği.</typeparam>
public abstract class RoomWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IRoomWriteRequest
{
    /// <summary>Sözleşmedeki oda numarası uzunluk sınırı (DB kolonu 16'ya kadar izin verir).</summary>
    private const int MaxNumberLength = 10;

    private const int MaxNoteLength = 500;

    private const int MinFloor = -5;

    private const int MaxFloor = 99;

    protected RoomWriteValidator()
    {
        RuleFor(request => request.Number)
            .NotEmpty()
            .MaximumLength(MaxNumberLength);

        RuleFor(request => request.Floor)
            .InclusiveBetween(MinFloor, MaxFloor);

        RuleFor(request => request.RoomTypeId)
            .NotEmpty();

        RuleFor(request => request.Note)
            .MaximumLength(MaxNoteLength);
    }
}
