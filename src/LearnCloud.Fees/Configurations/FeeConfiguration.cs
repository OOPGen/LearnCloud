using LearnCloud.Fees.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearnCloud.Fees.Configurations;

// DB FIX H8: Add HasPrecision for money columns to avoid default decimal(65,30)
public class FeeStructureItemConfiguration : IEntityTypeConfiguration<FeeStructureItem>
{
    public void Configure(EntityTypeBuilder<FeeStructureItem> b)
    {
        b.Property(x => x.Amount).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
        b.Property(x => x.LineTotal).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
    }
}

public class FeeInvoiceConfiguration : IEntityTypeConfiguration<FeeInvoice>
{
    public void Configure(EntityTypeBuilder<FeeInvoice> b)
    {
        b.Property(x => x.SubtotalAmount).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
        b.Property(x => x.DiscountAmount).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
        b.Property(x => x.TotalAmount).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
        b.Property(x => x.AmountPaid).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
        b.Property(x => x.BalanceDue).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
        // Unique per tenant + invoice_number, but should allow reuse after soft delete? Keep unique as is for now, app handles rename
        b.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique().HasDatabaseName("uq_invoices_tenant_number");
        // Covering index for parent portal - DB FIX H6
        b.HasIndex(x => new { x.TenantId, x.StudentId, x.Status, x.DueDate }).HasDatabaseName("idx_inv_tenant_student_status_due");
    }
}

public class FeeInvoiceItemConfiguration : IEntityTypeConfiguration<FeeInvoiceItem>
{
    public void Configure(EntityTypeBuilder<FeeInvoiceItem> b)
    {
        b.Property(x => x.UnitAmount).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
        b.Property(x => x.LineTotal).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(x => x.Amount).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
    }
}

public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> b)
    {
        b.Property(x => x.AllocatedAmount).HasPrecision(18,2).HasColumnType("DECIMAL(18,2)");
    }
}
