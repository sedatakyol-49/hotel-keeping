using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Invoices.AddPayment;
using HotelCore.Application.Features.Invoices.Cancel;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Application.Features.Invoices.Create;
using HotelCore.Application.Features.Invoices.Finalize;
using HotelCore.Application.Features.Invoices.GetById;
using HotelCore.Application.Features.Invoices.List;
using HotelCore.Application.Features.Invoices.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Fatura uç noktaları (docs/api-contracts-invoices.md) — <b>GoBD uyumlu</b> (architecture.md §6).
/// <para>
/// Yaşam döngüsü: <c>Draft</c> → (finalize) <c>Finalized</c> → (ödeme) <c>Paid</c>;
/// iptal her aşamada mümkündür ancak kesinleşmiş faturada <b>Stornorechnung</b> üretir.
/// Kesinleşmiş fatura <b>hiçbir uçtan değiştirilemez veya silinemez</b> — DELETE uç noktası
/// bilinçli olarak <b>yoktur</b> (10 yıl saklama, §6.4).
/// </para>
/// <para>
/// İzinler: okuma <c>Invoices.View</c>, taslak yazma <c>Invoices.Create</c>, kesinleştirme
/// <c>Invoices.Approve</c>, iptal <c>Invoices.Cancel</c> — rol adları controller'a
/// hardcode edilmez, policy adı = izin anahtarı (§7).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/invoices")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class InvoicesController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sayfalı + filtreli fatura listesi (en yeni fatura tarihi önce).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.InvoicesView)]
    [ProducesResponseType<PagedResult<InvoiceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<InvoiceResponse>> List(
        [FromQuery] ListInvoicesRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Fatura detayı: satırlar, ödemeler ve denetim izi (GoBD §6.3) dâhil.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetInvoiceById))]
    [Authorize(Policy = Permissions.InvoicesView)]
    [ProducesResponseType<InvoiceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<InvoiceDetailResponse> GetInvoiceById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetInvoiceByIdRequest(id), cancellationToken);

    /// <summary>Taslak fatura oluşturur (numara atanmaz).</summary>
    /// <remarks>
    /// İki yol birbirini dışlar: <c>reservationId</c> (oda ücreti + folio ekstraları + Kurtaxe
    /// sunucuda üretilir) veya <c>lineItems</c> + <c>guestId</c> (elle giriş). Tutarlar ve KDV
    /// oranları <b>sunucuda</b> hesaplanır; istemciden gelen toplamlar dikkate alınmaz.
    /// Yazma işlemi aktif otel gerektirir (<c>X-Hotel-Id</c>; konsolide modda 400).
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.InvoicesCreate)]
    [ProducesResponseType<InvoiceDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceDetailResponse>> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetInvoiceById), new { id = created.Id }, created);
    }

    /// <summary>Taslak faturayı günceller. <b>Yalnızca Draft</b>; kesinleşmiş faturada 409.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.InvoicesCreate)]
    [ProducesResponseType<InvoiceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<InvoiceDetailResponse> Update(
        Guid id,
        [FromBody] UpdateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kimlik route'tan gelir; gövdedeki bir "id" alanı dikkate alınmaz.
        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>
    /// Faturayı kesinleştirir: boşluksuz numara atanır, <c>IssuedAt</c> damgalanır, durum
    /// <c>Finalized</c> olur. Bundan sonra fatura <b>değiştirilemez</b> (GoBD §6.1).
    /// </summary>
    /// <remarks>
    /// Eşzamanlı bir başka kesinleştirme numara sekansını kilitlemişse <b>409</b> döner ve
    /// <b>hiçbir numara tüketilmez</b> — istek tekrarlanabilir.
    /// </remarks>
    [HttpPost("{id:guid}/finalize")]
    [Authorize(Policy = Permissions.InvoicesApprove)]
    [ProducesResponseType<InvoiceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<InvoiceDetailResponse> Finalize(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new FinalizeInvoiceRequest(id), cancellationToken);

    /// <summary>
    /// Faturayı iptal eder. Taslak doğrudan iptal edilir; kesinleşmiş/ödenmiş fatura için
    /// <b>Stornorechnung</b> (negatif tutarlı yeni fatura) kesilir ve orijinal ona
    /// <c>cancelledByInvoiceId</c> ile bağlanır. Orijinal <b>korunur</b>.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.InvoicesCancel)]
    [ProducesResponseType<InvoiceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<InvoiceDetailResponse> Cancel(
        Guid id,
        [FromBody] CancelInvoiceRequest? request,
        CancellationToken cancellationToken) =>
        dispatcher.Send((request ?? new CancelInvoiceRequest()) with { Id = id }, cancellationToken);

    /// <summary>
    /// Faturaya ödeme kaydeder. Toplam ödeme brüt tutara ulaşınca durum <c>Paid</c> olur;
    /// fazla ödeme <b>409</b> ile reddedilir.
    /// </summary>
    /// <remarks>
    /// Yanıt <b>faturanın güncel detayıdır</b> (ödeme ayrı adreslenebilir bir kaynak değildir),
    /// bu yüzden 201 değil 200 döner.
    /// </remarks>
    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = Permissions.InvoicesCreate)]
    [ProducesResponseType<InvoiceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<InvoiceDetailResponse> AddPayment(
        Guid id,
        [FromBody] AddInvoicePaymentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return dispatcher.Send(request with { InvoiceId = id }, cancellationToken);
    }

    /// <summary>
    /// Fatura PDF'i — <b>bu fazda uygulanmadı</b>, her zaman <b>501 Not Implemented</b> döner.
    /// </summary>
    /// <remarks>
    /// Sahte veya boş bir PDF döndürmek denetim açısından yanıltıcı olurdu. Üretim portu
    /// <see cref="IInvoiceExporter"/> olarak tanımlıdır (ZUGFeRD/XRechnung zemini,
    /// architecture.md §6.5) ama DI'a kayıtlı bir implementasyonu yoktur. Fatura verisi
    /// yapılandırılmış biçimde <c>GET /invoices/{id}</c> ile alınabilir.
    /// </remarks>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = Permissions.InvoicesView)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented)]
    public ActionResult Pdf(Guid id) =>
        StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = Messages.InvoicePdfNotImplementedTitle,
            Detail = Messages.InvoicePdfNotImplementedDetail(id),
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.2",
            Instance = $"{Request.Method} {Request.Path}"
        });
}
