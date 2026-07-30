using System.Globalization;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.AddPayment;

/// <summary>
/// Faturaya ödeme kaydeder ve gerekirse durumu <c>Paid</c>'e taşır.
///
/// <para><b>Ödeme/Paid mantığı:</b>
/// <list type="bullet">
///   <item>Ödeme yalnızca <c>Finalized</c> faturaya kaydedilebilir. Taslak bir belge değildir →
///   <b>409</b> ("önce finalize edin"). <c>Cancelled</c> → 409. <c>Paid</c> (tamamen ödenmiş) →
///   409.</item>
///   <item><b>Kısmi ödeme</b> serbesttir: toplam &lt; brüt ise durum <c>Finalized</c> kalır,
///   yanıtta <c>paidAmount</c>/<c>outstandingAmount</c> güncellenir.</item>
///   <item>Toplam ödeme brüt tutara <b>eşitlendiğinde</b> <c>MarkPaid()</c> çağrılır.</item>
///   <item><b>Fazla ödeme 409</b>: kalan bakiyeden büyük tutar reddedilir. Gerekçe: fatura
///   tutarını aşan para hareketi bir <i>avans/alacak</i> kaydıdır, faturanın parçası değildir;
///   sessizce kabul etmek fatura ile tahsilatı uyumsuz bırakır (GoBD izlenebilirlik). Kuruş
///   toleransı <b>yoktur</b>: tutarlar 2 ondalık ve tam karşılaştırılır.</item>
///   <item>Negatif tutarlı belgeye (Stornorechnung) ödeme kaydedilemez — <b>iade akışı bu fazda
///   yok</b>, 409 döner.</item>
/// </list></para>
///
/// <para><b>Denetim izi:</b> her ödeme <see cref="InvoiceAuditAction.PaymentRecorded"/> olarak
/// yazılır (tahsilat <i>olayı</i>: tutar, toplam ödenen, kalan bakiye). Bakiye kapandığında buna
/// <b>ek olarak</b> <see cref="InvoiceAuditAction.Paid"/> yazılır (durum <i>geçişi</i>). İki kayıt
/// ayrı olduğu için "bakiye ne zaman kapandı?" sorusu JSON ayrıntısı ayrıştırılmadan
/// yanıtlanabilir.</para>
///
/// <para><b>Atomiklik:</b> ödeme kaydı + durum geçişi + <c>InvoiceAuditEntry</c>'ler tek
/// <c>SaveChanges</c> içinde yazılır.</para>
/// </summary>
internal sealed class AddInvoicePaymentHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    InvoiceReader reader,
    InvoiceAuditWriter audit)
    : IRequestHandler<AddInvoicePaymentRequest, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        AddInvoicePaymentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoice = await reader.GetTrackedAsync(request.InvoiceId, cancellationToken)
            .ConfigureAwait(false);

        EnsurePayable(invoice);

        var now = clock.UtcNow;
        var paidAt = request.PaidAt ?? now;

        if (paidAt > now)
        {
            throw new ValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["PaidAt"] = ["Odeme zamani gelecekte olamaz."]
            });
        }

        // Odenen toplam VERITABANINDAN okunur; istemcinin bildirdigi bakiyeye guvenilmez.
        var alreadyPaid = await reader.GetPaidAmountAsync(invoice.Id, cancellationToken)
            .ConfigureAwait(false);

        var amount = InvoiceAmounts.Round(request.Amount);
        var outstanding = invoice.GrossAmount - alreadyPaid;

        if (amount > outstanding)
        {
            throw new ConflictException(string.Format(
                CultureInfo.InvariantCulture,
                "Odeme tutari kalan bakiyeyi asiyor. Kalan: {0:0.00} {1}, gonderilen: {2:0.00} {1}. " +
                "Fazla odeme faturaya kaydedilemez.",
                outstanding,
                invoice.Currency,
                amount));
        }

        var payment = new Payment
        {
            // HotelId aktif otelden degil FATURADAN alinir (konsolide mod guvenligi).
            HotelId = invoice.HotelId,
            InvoiceId = invoice.Id,
            Method = request.Method,
            Amount = amount,
            PaidAt = paidAt,
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
        };

        database.Payments.Add(payment);

        var totalPaid = alreadyPaid + amount;
        var outstandingAfter = invoice.GrossAmount - totalPaid;
        var fullySettled = totalPaid >= invoice.GrossAmount;

        // 1) TAHSILAT OLAYI: her odeme (kismi ya da tam) ayni aksiyonla yazilir.
        audit.Append(invoice, InvoiceAuditAction.PaymentRecorded, new
        {
            paymentId = payment.Id,
            method = payment.Method.ToString(),
            amount = payment.Amount,
            paidAt = payment.PaidAt,
            reference = payment.Reference,
            totalPaid,
            grossAmount = invoice.GrossAmount,
            outstandingAmount = outstandingAfter,
            currency = invoice.Currency
        });

        // 2) DURUM GECISI: yalnizca bakiye kapandiginda, ayri bir kayit olarak.
        if (fullySettled)
        {
            var previousStatus = invoice.Status;

            invoice.MarkPaid();

            audit.Append(invoice, InvoiceAuditAction.Paid, new
            {
                previousStatus = previousStatus.ToString(),
                status = invoice.Status.ToString(),
                settledByPaymentId = payment.Id,
                totalPaid,
                grossAmount = invoice.GrossAmount,
                currency = invoice.Currency
            });
        }

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetDetailAsync(invoice.Id, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsurePayable(Invoice invoice)
    {
        switch (invoice.Status)
        {
            case InvoiceStatus.Draft:
                throw new ConflictException(
                    "Taslak faturaya odeme kaydedilemez. Once faturayi kesinlestirin (finalize).");
            case InvoiceStatus.Cancelled:
                throw new ConflictException("Iptal edilmis faturaya odeme kaydedilemez.");
            case InvoiceStatus.Paid:
                throw new ConflictException("Fatura zaten tamamen odenmis.");
            case InvoiceStatus.Finalized:
                break;
            default:
                throw new ConflictException($"Bu durumda odeme kaydedilemez: {invoice.Status}.");
        }

        if (invoice.GrossAmount <= 0m)
        {
            throw new ConflictException(
                "Tutari sifir veya negatif olan belgeye (orn. iptal faturasi) odeme kaydedilemez; " +
                "iade akisi bu fazda desteklenmiyor.");
        }
    }
}
