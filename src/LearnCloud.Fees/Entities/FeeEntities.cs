using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Fees.Entities;

public enum FeeItemRecurrence { PerTerm = 1, PerYear = 2, OneOff = 3 }
public enum DiscountType { Percentage = 1, FixedAmount = 2 }
public enum InvoiceStatus { Draft = 1, Issued = 2, Partial = 3, Paid = 4, Overdue = 5, Void = 6, Credit = 7 }
public enum PaymentMethod { Cash = 1, BankTransfer = 2, EcoCash = 3, OneMoney = 4, PayNow = 5, Card = 6, Other = 7 }
public enum PaymentStatus { Pending = 1, Confirmed = 2, Reversed = 3 }

// Fee items (tuition, boarding, transport, levy, uniform), each recurring per term, per year, or one-off
public class FeeItem : TenantOwnedEntity
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!; // TUITION, BOARDING, TRANSPORT, LEVY, UNIFORM
    public FeeItemRecurrence Recurrence { get; set; } = FeeItemRecurrence.PerTerm;
    public bool IsProratable { get; set; } = false;
    public bool IsOptional { get; set; } = false;
    public string? GlCode { get; set; }
    public string? Description { get; set; }
}

// Fee structures assigning fee items and amounts to a class, stream or individual learner for a given academic year and term
public class FeeStructure : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. Term 1 2026 Grade 5 Fees
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long? GradeId { get; set; } // null = school-wide
    public long? StreamId { get; set; } // null = grade-wide
    public long? StudentId { get; set; } // null = not individual, if set = individual override
    public string Status { get; set; } = "draft"; // draft, active, archived
    public string Currency { get; set; } = "USD";
    public bool IsMandatory { get; set; } = true;

    public ICollection<FeeStructureItem> Items { get; set; } = new List<FeeStructureItem>();
}

public class FeeStructureItem : TenantOwnedEntity
{
    public long FeeStructureId { get; set; }
    public FeeStructure FeeStructure { get; set; } = null!;
    public long FeeItemId { get; set; }
    public FeeItem FeeItem { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; } // DECIMAL(18,2)
    public string Currency { get; set; } = "USD";
    public int Quantity { get; set; } = 1;
    public decimal LineTotal { get; set; } // Amount * Quantity
}

// Discounts and scholarships as percentage or fixed amount, applied to learner for period, with reason and approver
public class Discount : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public DiscountType Type { get; set; }
    public decimal Value { get; set; } // e.g. 10 for 10% or 100 for $100 fixed, DECIMAL(18,2)
    public string Currency { get; set; } = "USD"; // for fixed
    public string? AppliesToFeeItemIdsJson { get; set; } // JSON list of fee_item_ids, null = total
    public string Reason { get; set; } = null!; // Mid-term join, Sibling, Staff child
    public long ApproverUserId { get; set; }
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "approved"; // pending, approved, rejected
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

// Invoices generated per learner per term from applicable structure, with line items, invoice number from configurable sequence
public class InvoiceSequence : TenantOwnedEntity
{
    public int Year { get; set; }
    public int LastNumber { get; set; } = 0;
    public string Prefix { get; set; } = "INV";
    public string Format { get; set; } = "{prefix}-{year}-{number:5}"; // INV-2026-00001
}

public class ReceiptSequence : TenantOwnedEntity
{
    public int Year { get; set; }
    public int LastNumber { get; set; } = 0;
    public string Prefix { get; set; } = "REC";
    public string Format { get; set; } = "{prefix}-{year}-{number:5}";
}

public class FeeInvoice : TenantOwnedEntity
{
    public string InvoiceNumber { get; set; } = null!; // INV-2026-00001 unique per tenant per year
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long StudentId { get; set; }
    public long? EnrolmentId { get; set; }
    public long? FeeStructureId { get; set; } // source structure hash for idempotency
    public string StructureHash { get; set; } = null!; // hash of structure amounts for idempotent generation

    public decimal SubtotalAmount { get; set; } // sum lines
    public decimal DiscountAmount { get; set; } // sum discounts
    public decimal TotalAmount { get; set; } // subtotal - discount
    public decimal AmountPaid { get; set; } // sum allocations
    public decimal BalanceDue { get; set; } // total - paid
    public string Currency { get; set; } = "USD";

    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    public bool IsProrated { get; set; } = false;
    public string? ProrationNote { get; set; }

    public ICollection<FeeInvoiceItem> LineItems { get; set; } = new List<FeeInvoiceItem>();
    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}

public class FeeInvoiceItem : TenantOwnedEntity
{
    public long InvoiceId { get; set; }
    public FeeInvoice Invoice { get; set; } = null!;
    public long? FeeStructureItemId { get; set; } // snapshot source, null if custom
    public long? FeeItemId { get; set; }
    public string Description { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public decimal UnitAmount { get; set; } // DECIMAL(18,2)
    public decimal LineTotal { get; set; } // unit*quantity
    public string Currency { get; set; } = "USD";
    public bool IsProrated { get; set; } = false;
    public string? ProrationDetail { get; set; }
}

// Payments recorded against learner with method, reference, date and receipt number, then allocated
public class Payment : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public decimal Amount { get; set; } // DECIMAL(18,2)
    public string Currency { get; set; } = "USD";
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? Reference { get; set; }
    public DateTime PaymentDate { get; set; }
    public string ReceiptNumber { get; set; } = null!; // REC-2026-00001
    public string? ProofUrl { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Confirmed;
    public long? ReversedByPaymentId { get; set; } // for reversal entry
    public long? OriginalPaymentId { get; set; } // if this is reversal of original

    public string? ReversalReason { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}

// Explicit rule oldest first, manual override, supports part-payments and overpayments held as credit
public class PaymentAllocation : TenantOwnedEntity
{
    public long PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public long InvoiceId { get; set; }
    public FeeInvoice Invoice { get; set; } = null!;
    public long? InvoiceItemId { get; set; } // optional line-level allocation
    public decimal AllocatedAmount { get; set; } // DECIMAL(18,2)
    public string Currency { get; set; } = "USD";
    public bool IsManualOverride { get; set; } = false;
    public bool IsReversal { get; set; } = false; // true if this allocation reverses previous
}

// Overpayments held as credit
public class LearnerCredit : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public decimal Amount { get; set; } // positive = credit held
    public string Currency { get; set; } = "USD";
    public string Source { get; set; } = "overpayment"; // overpayment, credit_note
    public long? SourcePaymentId { get; set; }
    public long? SourceCreditNoteId { get; set; }
    public bool IsUtilized { get; set; } = false;
    public DateTime? UtilizedAt { get; set; }
}

// Credit notes and adjustments, each requiring reason and creating audit entry
public class CreditNote : TenantOwnedEntity
{
    public long InvoiceId { get; set; }
    public FeeInvoice Invoice { get; set; } = null!;
    public long StudentId { get; set; }
    public decimal Amount { get; set; } // positive amount to credit (reduce balance)
    public string Currency { get; set; } = "USD";
    public string Reason { get; set; } = null!; // Early withdrawal, Overcharge correction
    public long ApproverUserId { get; set; }
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "approved"; // pending, approved, rejected
    public string CreditNoteNumber { get; set; } = null!; // CN-2026-00001
}

// Background job idempotent reporting
public class FeeInvoiceBatch : TenantOwnedEntity
{
    public string BatchNumber { get; set; } = null!; // BATCH-2026-T2-001
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public string Status { get; set; } = "pending"; // pending, running, completed, failed
    public int TotalStudents { get; set; }
    public int Processed { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public string? ResultJson { get; set; } // {created:[...], skipped:[{studentId, reason}]}
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProgressPercent { get; set; }
}
