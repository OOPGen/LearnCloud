namespace LearnCloud.Finance.DTOs;

public record ExpenseCategoryDto(long Id, string Name, string Code, string Type, long? ParentId, bool IsActive);
public record CreateExpenseCategoryRequest(string Name, string Code, string Type, long? ParentId, string? Description, string? GlCode);

public record SupplierDto(long Id, string Name, string Code, string? ContactPerson, string? Email, string? Phone, string? TaxId, bool IsActive, decimal TotalPurchases, string Currency);
public record CreateSupplierRequest(string Name, string Code, string? ContactPerson, string? Email, string? Phone, string? Address, string? TaxId, string? BankAccountNumber, string? BankName);

public record ExpenseDto(long Id, string ExpenseNumber, long CategoryId, string CategoryName, long? SupplierId, string? SupplierName, string Description, decimal Amount, string Currency, DateTime ExpenseDate, string Status, string? PaymentMethod, string? SupportingDocumentUrl, string? Notes, long CreatedByUserId, long? ApprovedByUserId);
public record CreateExpenseRequest(long CategoryId, long? SupplierId, string Description, decimal Amount, string Currency, DateTime ExpenseDate, long? AcademicYearId, long? TermId, long? BudgetId, string? PaymentMethod, long? BankAccountId, string? SupportingDocumentUrl, string? Notes);

public record ApprovalThresholdDto(long Id, decimal MinAmount, decimal? MaxAmount, string Currency, string RequiredApproverRole, int RequiredApprovals, bool AutoApprove, int ApprovalOrder);
public record CreateApprovalThresholdRequest(decimal MinAmount, decimal? MaxAmount, string Currency, string RequiredApproverRole, int RequiredApprovals, bool AutoApprove, int ApprovalOrder, string? Description);

public record ApprovalRequestDto(long Id, long ExpenseId, long ThresholdId, long ApproverUserId, string ApproverRole, string Status, string? Comment, DateTime? DecidedAt, int ApprovalOrder);
public record ApproveExpenseRequest(string Status, string? Comment); // approved/rejected

public record BudgetDto(long Id, string Name, long AcademicYearId, long TermId, long CategoryId, string CategoryName, decimal BudgetedAmount, decimal ActualAmount, decimal VarianceAmount, decimal VariancePercentage, string Currency, string Status);
public record CreateBudgetRequest(string Name, long AcademicYearId, long TermId, long CategoryId, decimal BudgetedAmount, string Currency, string? Notes);
public record BudgetVarianceReportDto(long CategoryId, string CategoryName, decimal Budgeted, decimal Actual, decimal VarianceAmount, decimal VariancePercentage, string Currency, string Status);

public record BankAccountDto(long Id, string Name, string AccountNumber, string BankName, string Currency, decimal OpeningBalance, decimal CurrentBalance, bool IsActive, string AccountType);
public record CreateBankAccountRequest(string Name, string AccountNumber, string BankName, string Currency, decimal OpeningBalance, string AccountType);

public record CashBookEntryDto(long Id, long BankAccountId, string BankAccountName, DateTime EntryDate, string Description, string Reference, decimal Debit, decimal Credit, decimal Balance, string Currency, string EntryType, bool IsReconciled);
public record CreateCashBookEntryRequest(long BankAccountId, DateTime EntryDate, string Description, string Reference, decimal Debit, decimal Credit, string Currency, string EntryType, long? RelatedExpenseId, long? RelatedFeePaymentId, long? TransferToAccountId);

public record BankStatementDto(long Id, long BankAccountId, string FileName, DateTime StatementDate, DateTime FromDate, DateTime ToDate, decimal OpeningBalance, decimal ClosingBalance, string Currency, string Status);
public record ReconcileRequest(long BankStatementLineId, long CashBookEntryId);

public record PettyCashAccountDto(long Id, string Name, decimal FloatAmount, decimal CurrentBalance, string Currency, long CustodianUserId, bool IsActive);
public record CreatePettyCashAccountRequest(string Name, decimal FloatAmount, string Currency, long CustodianUserId);
public record PettyCashDisbursementDto(long Id, long PettyCashAccountId, decimal Amount, string Currency, string Description, string Recipient, DateTime DisbursementDate, string Status, string? ReceiptUrl);
public record CreatePettyCashDisbursementRequest(long PettyCashAccountId, decimal Amount, string Currency, string Description, string Recipient, DateTime DisbursementDate, string? ReceiptUrl, long? RelatedExpenseId);
public record PettyCashReconciliationDto(long Id, long PettyCashAccountId, DateTime ReconciliationDate, decimal FloatAmount, decimal DisbursedTotal, decimal CashCounted, decimal Variance, string Status, string? Notes);

public record PeriodLockDto(long Id, long AcademicYearId, long TermId, bool IsLocked, DateTime? LockedAt, long? LockedByUserId, string? LockReason, bool IsUnlocked, DateTime? UnlockedAt, long? UnlockedByUserId, string? UnlockReason);
public record LockPeriodRequest(long AcademicYearId, long TermId, string? LockReason);
public record UnlockPeriodRequest(long AcademicYearId, long TermId, string UnlockReason, string UnlockApproverRole);

public record FinancialReportRequest(long? AcademicYearId, long? TermId, DateTime? FromDate, DateTime? ToDate, long? CategoryId, long? GradeId, long? StreamId);
public record IncomeExpenditureReportDto(DateTime FromDate, DateTime ToDate, decimal TotalIncome, decimal FeeCollection, decimal OtherIncome, decimal TotalExpenditure, decimal Net, string Currency, List<CategoryBreakdownDto> Breakdown);
public record CategoryBreakdownDto(long CategoryId, string CategoryName, decimal Budgeted, decimal Actual, decimal VarianceAmount, decimal VariancePercentage);
public record FeeCollectionSummaryDto(decimal TotalInvoiced, decimal TotalCollected, decimal TotalArrears, decimal CollectionRate, string Currency, List<ClassCollectionRateDto> ByClass);
public record ClassCollectionRateDto(long GradeId, string GradeName, long StreamId, string StreamName, decimal Invoiced, decimal Collected, decimal Rate, decimal Arrears);
public record ArrearsAgeingDto(decimal Current, decimal Days30, decimal Days60, decimal Days90, decimal Total, string Currency, List<ArrearsStudentDto> Students);
public record ArrearsStudentDto(long StudentId, string StudentName, string StudentNumber, string GradeName, string StreamName, decimal Balance, int DaysOverdue, string Currency);
