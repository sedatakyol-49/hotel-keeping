---
name: backend-dotnet-ef
description: .NET 10 ASP.NET Core Web API — Clean Architecture, vertical-slice handler, Mapster, FluentValidation, JWT + policy-based RBAC, Serilog, ProblemDetails. Controller/handler/DTO/validation/auth işlerinde tetiklenir.
---

# Skill: Backend (.NET + EF)

## Tetikleyici senaryolar
Use-case handler, DTO, validator, controller, auth policy, middleware, OpenAPI güncelleme.

## Vertical slice yapısı
```
Application/Features/<Module>/<Action>/
  <Action>Request.cs      # IRequest<TResponse>
  <Action>Handler.cs      # IRequestHandler<Request, Response>
  <Action>Validator.cs    # AbstractValidator<Request>
  <Action>Response.cs     # DTO
```
Dispatcher pipeline: validation → handler. `new` yok, DI ile çözülür.

## Örnek handler
```csharp
public sealed record CheckInReservationRequest(Guid ReservationId) : IRequest<Unit>;

public sealed class CheckInReservationHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<CheckInReservationRequest, Unit>
{
    public async Task<Unit> Handle(CheckInReservationRequest req, CancellationToken ct)
    {
        var res = await db.Reservations.FirstOrDefaultAsync(x => x.Id == req.ReservationId, ct)
            ?? throw new NotFoundException(nameof(Reservation), req.ReservationId);
        res.CheckIn();                 // domain davranışı entity'de
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
```

## Controller
```csharp
[ApiController]
[Route("api/v1/reservations")]
[Authorize]
public sealed class ReservationsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost("{id:guid}/check-in")]
    [Authorize(Policy = Permissions.ReservationsCheckInOut)]
    public Task<Unit> CheckIn(Guid id) => dispatcher.Send(new CheckInReservationRequest(id));
}
```

## Kurallar
- Roller controller'a hardcode edilmez → `[Authorize(Policy = ...)]`.
- Mapping: Mapster. Validation: FluentValidation. Hata: ProblemDetails.
- Multi-tenant filtre elle atlanmaz. GoBD guard'ları faturaya dokununca kontrol edilir.

## Komutlar
`dotnet build`, `dotnet run --project HotelCore.Api`, `dotnet test` (cwd: `src/backend`).
