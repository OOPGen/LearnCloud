using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Finance.Entities;

// Expense categories
public class ExpenseCategory : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. Teaching Materials, Utilities, Maintenance
    public string Code { get; set; } = null!; // TEACH_MAT, UTIL, MAINT
    public string Type { get; set; } = "operational"; // operational, capital, administrative, academic, welfare
    public long? ParentCategoryId { get; set; }
    public ExpenseCategory? ParentCategory { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string? GlCode { get; set; }
    public ICollection<ExpenseCategory> Children { get; set; } = new List<ExpenseCategory>();
}

// Approval workflow thresholds - configurable
public class ApprovalThreshold : TenantOwnedEntity
{
    public decimal MinAmount { get; set; } // inclusive
    public decimal? MaxAmount { get; set; } // exclusive, null = no upper limit
    public string Currency { get; set; } = "USD";
    public string RequiredApproverRole { get; set; } = null!; // e.g. BURSAR, HEAD_TEACHER, DIRECTOR, BOARD
    public int RequiredApprovals { get; set; } = 1; // how many approvers needed at this level
    public bool AutoApprove { get; set; } = false; // if true and amount in range, auto-approved
    public int ApprovalOrder { get; set; } = 1; // order in workflow chain
    public string? Description { get; set; }
}

// Expense capture with supporting document upload
public class Expense : TenantOwnedEntity
{
    public string ExpenseNumber { get; set; } = null!; // EXP-2026-00001
    public long CategoryId { get; set; }
    public ExpenseCategory Category { get; set; } = null!;
    public long? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public long? PurchaseRecordId { get; set; }
    public PurchaseRecord? PurchaseRecord { get; set; }

    public string Description { get; set; } = null!;
    public decimal Amount { get; set; } // DECIMAL(18,2)
    public string Currency { get; set; } = "USD";
    public DateTime ExpenseDate { get; set; }
    public long? AcademicYearId { get; set; }
    public long? TermId { get; set; }
    public long? BudgetId { get; set; }

    public string Status { get; set; } = "draft"; // draft, pending_approval, approved, rejected, paid, cancelled
    public string? PaymentMethod { get; set; } // cash, bank_transfer, petty_cash, mobile_money
    public long? BankAccountId { get; set; }
    public long? PettyCashDisbursementId { get; set; }

    public string? SupportingDocumentUrl { get; set; } // main document
    public string? Notes { get; set; }

    public long CreatedByUserId { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public ICollection<ExpenseDocument> Documents { get; set; } = new List<ExpenseDocument>();
    public ICollection<ApprovalRequest> Approvals { get; set; } = new List<ApprovalRequest>();
}

public class ExpenseDocument : TenantOwnedEntity
{
    public long ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public string? Description { get; set; }
    public long UploadedByUserId { get; set; }
}

public class ApprovalRequest : TenantOwnedEntity
{
    public long ExpenseId { get; set; }
    public Expense Expense { get; set; } = null!;
    public long ThresholdId { get; set; }
    public ApprovalThreshold Threshold { get; set; } = null!;
    public long ApproverUserId { get; set; }
    public string ApproverRole { get; set; } = null!;
    public string Status { get; set; } = "pending"; // pending, approved, rejected
    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int ApprovalOrder { get; set; }
}

// Suppliers and purchase records
public class Supplier : TenantOwnedEntity
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!; // SUP-001
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal TotalPurchases { get; set; } // cached sum
    public string Currency { get; set; } = "USD";
}

public class PurchaseRecord : TenantOwnedEntity
{
    public string PurchaseNumber { get; set; } = null!; // PO-2026-00001
    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public DateTime PurchaseDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "draft"; // draft, ordered, received, paid, cancelled
    public string? Notes { get; set; }
    public long CreatedByUserId { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}

public class PurchaseItem : TenantOwnedEntity
{
    public long PurchaseRecordId { get; set; }
    public PurchaseRecord PurchaseRecord { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string Currency { get; set; } = "USD";
    public long? ExpenseCategoryId { get; set; }
}

// Budgets per category per term, with actual vs budget
public class Budget : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. Term 2 2026 Budget
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long CategoryId { get; set; }
    public ExpenseCategory Category { get; set; } = null!;
    public decimal BudgetedAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ActualAmount { get; set; } // computed sum of approved expenses in category/term
    public decimal VarianceAmount { get; set; } // Budgeted - Actual
    public decimal VariancePercentage { get; set; } // Variance / Budgeted *100
    public string Status { get; set; } = "draft"; // draft, approved, closed
    public string? Notes { get; set; }
}

// Cash book and bank accounts, with recording of transfers and reconciliation against a statement
public class BankAccount : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. Main USD Account
    public string AccountNumber { get; set; } = null!; // e.g. 123456789
    public string BankName { get; set; } = null!; // e.g. CBZ, Stanbic
    public string Currency { get; set; } = "USD";
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public string AccountType { get; set; } = "bank"; // bank, cash, mobile_money

    public ICollection<CashBookEntry> Entries { get; set; } = new List<CashBookEntry>();
}

public class CashBookEntry : TenantOwnedEntity
{
    public long BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = null!;
    public string Reference { get; set; } = null!; // e.g. fee payment ref, expense number
    public decimal Debit { get; set; } // money out
    public decimal Credit { get; set; } // money in
    public decimal Balance { get; set; } // running balance
    public string Currency { get; set; } = "USD";
    public string EntryType { get; set; } = "general"; // income (fees), expense, transfer, opening
    public long? RelatedExpenseId { get; set; }
    public long? RelatedFeePaymentId { get; set; } // link to existing fee Payment entity without altering it
    public long? RelatedPlatformInvoiceId { get; set; } // link to platform invoice if needed
    public long? TransferToAccountId { get; set; } // for transfers
    public string? TransferId { get; set; } // group id for double entry transfer
    public bool IsReconciled { get; set; } = false;
    public DateTime? ReconciledAt { get; set; }
    public long? ReconciledByUserId { get; set; }
}

public class BankStatement : TenantOwnedEntity
{
    public long BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    public string FileName { get; set; } = null!; // uploaded statement file
    public string FileUrl { get; set; } = null!;
    public DateTime StatementDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending"; // pending, reconciled, archived
    public ICollection<BankStatementLine> Lines { get; set; } = new List<BankStatementLine>();
}

public class BankStatementLine : TenantOwnedEntity
{
    public long BankStatementId { get; set; }
    public BankStatement BankStatement { get; set; } = null!;
    public long BankAccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; } // positive = credit, negative = debit? Or separate debit/credit
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsReconciled { get; set; } = false;
    public long? MatchedCashBookEntryId { get; set; }
    public CashBookEntry? MatchedEntry { get; set; }
}

// Petty cash with float, disbursement and reconciliation
public class PettyCashAccount : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // Main Petty Cash
    public decimal FloatAmount { get; set; } // e.g. 200 USD float
    public decimal CurrentBalance { get; set; }
    public string Currency { get; set; } = "USD";
    public long CustodianUserId { get; set; } // who holds the cash
    public bool IsActive { get; set; } = true;

    public ICollection<PettyCashDisbursement> Disbursements { get; set; } = new List<PettyCashDisbursement>();
}

public class PettyCashDisbursement : TenantOwnedEntity
{
    public long PettyCashAccountId { get; set; }
    public PettyCashAccount PettyCashAccount { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Description { get; set; } = null!;
    public string Recipient { get; set; } = null!; // who received
    public DateTime DisbursementDate { get; set; }
    public string Status { get; set; } = "disbursed"; // disbursed, reconciled, voided
    public string? ReceiptUrl { get; set; }
    public long? RelatedExpenseId { get; set; }
    public long DisbursedByUserId { get; set; }
}

public class PettyCashReconciliation : TenantOwnedEntity
{
    public long PettyCashAccountId { get; set; }
    public PettyCashAccount PettyCashAccount { get; set; } = null!;
    public DateTime ReconciliationDate { get; set; }
    public decimal FloatAmount { get; set; }
    public decimal DisbursedTotal { get; set; } // sum of disbursements since last reconciliation
    public decimal CashCounted { get; set; } // physical cash counted
    public decimal Variance { get; set; } // CashCounted - (Float - DisbursedTotal)
    public string Status { get; set; } = "pending"; // pending, approved, variance_noted
    public string? Notes { get; set; }
    public long ReconciledByUserId { get; set; }
    public long? ApprovedByUserId { get; set; }
}

// Period locking so that a closed term cannot be edited, with documented and audited unlock requiring elevated permission
public class PeriodLock : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public bool IsLocked { get; set; } = false;
    public DateTime? LockedAt { get; set; }
    public long? LockedByUserId { get; set; }
    public string? LockReason { get; set; }

    public bool IsUnlocked { get; set; } = false;
    public DateTime? UnlockedAt { get; set; }
    public long? UnlockedByUserId { get; set; }
    public string? UnlockReason { get; set; } // documented and audited unlock requiring elevated permission
    public string? UnlockApproverRole { get; set; } // e.g. DIRECTOR, BOARD
}
