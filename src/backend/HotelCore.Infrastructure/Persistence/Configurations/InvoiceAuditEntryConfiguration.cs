using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelCore.Infrastructure.Persistence.Configurations;

public sealed class InvoiceAuditEntryConfiguration : IEntityTypeConfiguration<InvoiceAuditEntry>
{
    public void Configure(EntityTypeBuilder<InvoiceAuditEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        // JSON ayrıntı düz metin olarak saklanır; ileride jsonb'ye taşınabilir.
        builder.Property(x => x.Details).HasMaxLength(4000);

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.AuditEntries)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => new { x.HotelId, x.PerformedAt });
    }
}
