using FluentValidation;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.Update;

public sealed class UpdateInvoiceValidator : InvoiceWriteValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceValidator()
    {
        RuleFor(request => request.Id).NotEmpty();

        RuleFor(request => request.GuestId)
            .NotEqual(Guid.Empty)
            .When(request => request.GuestId is not null);

        // "Satirsiz fatura olmaz" kurali BURADA DEGIL, handler'da uygulanir (ayni 400 + ayni
        // "LineItems" anahtari + ayni mesaj). Gerekce: rezervasyondan uretilen faturada bos bir
        // lineItems dizisi mesru bir istektir ("elle eklenen tum ekstralari kaldir") — sunucunun
        // urettigi konaklama ve Kurtaxe satirlari zaten yerinde kalir. Elle kesilen faturada ise
        // bos govde gercekten satirsiz bir belge uretirdi. Ayrimi ancak faturanin kaynagini bilen
        // handler yapabilir; validator istegin govdesinden bunu goremez.
    }
}
