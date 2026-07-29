using AwesomeAssertions;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Tests.Entities;

/// <summary>
/// <see cref="Invoice"/> durum makinesi testleri (GoBD — architecture.md §6.1).
/// Gecerli gecisler: Draft -> Finalized -> Paid, Draft/Finalized/Paid -> Cancelled.
/// Kesinlesmis fatura yalnizca iptal faturasi (Stornorechnung) ile iptal edilebilir.
/// </summary>
public sealed class InvoiceStateTransitionTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private static Invoice NewDraft() => new();

    [Fact]
    public void New_invoice_starts_as_draft_without_number_or_issue_date()
    {
        var invoice = NewDraft();

        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.InvoiceNumber.Should().BeEmpty();
        invoice.IssuedAt.Should().BeNull();
    }

    [Fact]
    public void MarkFinalized_assigns_number_and_issue_date_and_locks_the_invoice()
    {
        var invoice = NewDraft();

        invoice.MarkFinalized("2026-000001", IssuedAt);

        invoice.Status.Should().Be(InvoiceStatus.Finalized);
        invoice.InvoiceNumber.Should().Be("2026-000001");
        invoice.IssuedAt.Should().Be(IssuedAt);
    }

    [Fact]
    public void MarkFinalized_is_rejected_when_the_invoice_is_not_a_draft()
    {
        var invoice = NewDraft();
        invoice.MarkFinalized("2026-000001", IssuedAt);

        var act = () => invoice.MarkFinalized("2026-000002", IssuedAt);

        act.Should().Throw<InvalidOperationException>();
        // Reddedilen gecis fatura numarasini bozmamali.
        invoice.InvoiceNumber.Should().Be("2026-000001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkFinalized_is_rejected_for_a_blank_invoice_number(string invoiceNumber)
    {
        var invoice = NewDraft();

        var act = () => invoice.MarkFinalized(invoiceNumber, IssuedAt);

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(invoiceNumber));
        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void MarkPaid_moves_a_finalized_invoice_to_paid()
    {
        var invoice = NewDraft();
        invoice.MarkFinalized("2026-000001", IssuedAt);

        invoice.MarkPaid();

        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void MarkPaid_is_rejected_for_a_draft_invoice()
    {
        var invoice = NewDraft();

        Action act = invoice.MarkPaid;

        act.Should().Throw<InvalidOperationException>();
        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void MarkPaid_is_rejected_for_a_cancelled_invoice()
    {
        var invoice = NewDraft();
        invoice.MarkCancelled();

        Action act = invoice.MarkPaid;

        act.Should().Throw<InvalidOperationException>();
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void MarkCancelled_cancels_a_draft_invoice_without_a_cancellation_invoice()
    {
        var invoice = NewDraft();

        invoice.MarkCancelled();

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        invoice.CancelledByInvoiceId.Should().BeNull();
    }

    [Fact]
    public void MarkCancelled_requires_a_cancellation_invoice_once_the_invoice_is_finalized()
    {
        var invoice = NewDraft();
        invoice.MarkFinalized("2026-000001", IssuedAt);

        var act = () => invoice.MarkCancelled();

        act.Should().Throw<InvalidOperationException>();
        invoice.Status.Should().Be(InvoiceStatus.Finalized);
    }

    [Fact]
    public void MarkCancelled_links_the_stornorechnung_when_cancelling_a_finalized_invoice()
    {
        var invoice = NewDraft();
        invoice.MarkFinalized("2026-000001", IssuedAt);
        var cancellationInvoiceId = Guid.NewGuid();

        invoice.MarkCancelled(cancellationInvoiceId);

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        invoice.CancelledByInvoiceId.Should().Be(cancellationInvoiceId);
        // Iptal, orijinal numarayi/tarihi silmez — GoBD izlenebilirligi korunur.
        invoice.InvoiceNumber.Should().Be("2026-000001");
        invoice.IssuedAt.Should().Be(IssuedAt);
    }

    [Fact]
    public void MarkCancelled_links_the_stornorechnung_when_cancelling_a_paid_invoice()
    {
        var invoice = NewDraft();
        invoice.MarkFinalized("2026-000001", IssuedAt);
        invoice.MarkPaid();
        var cancellationInvoiceId = Guid.NewGuid();

        invoice.MarkCancelled(cancellationInvoiceId);

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        invoice.CancelledByInvoiceId.Should().Be(cancellationInvoiceId);
    }

    [Fact]
    public void MarkCancelled_is_rejected_when_the_invoice_is_already_cancelled()
    {
        var invoice = NewDraft();
        invoice.MarkCancelled();

        var act = () => invoice.MarkCancelled(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFinalized_is_rejected_after_cancellation()
    {
        var invoice = NewDraft();
        invoice.MarkCancelled();

        var act = () => invoice.MarkFinalized("2026-000001", IssuedAt);

        act.Should().Throw<InvalidOperationException>();
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void Status_number_and_issue_date_cannot_be_set_from_outside_the_entity()
    {
        // Durum gecisleri yalnizca domain metotlariyla yapilabilmeli (GoBD guard'inin on kosulu).
        typeof(Invoice).GetProperty(nameof(Invoice.Status))!.SetMethod!.IsPublic.Should().BeFalse();
        typeof(Invoice).GetProperty(nameof(Invoice.IssuedAt))!.SetMethod!.IsPublic.Should().BeFalse();
        typeof(Invoice).GetProperty(nameof(Invoice.CancelledByInvoiceId))!.SetMethod!.IsPublic.Should().BeFalse();
    }
}
