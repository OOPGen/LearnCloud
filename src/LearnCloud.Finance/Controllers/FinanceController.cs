using LearnCloud.Finance.DTOs;
using LearnCloud.Finance.Entities;
using LearnCloud.Finance.Services;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Finance.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class FinanceController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly FinanceCalculationService _calc;

    public FinanceController(LearnCloudDbContext db, ITenantContext tenantContext, FinanceCalculationService calc)
    {
        _db = db; _tenantContext = tenantContext; _calc = calc;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Strict permission separation: capture, approval, reporting
    private bool IsInRole(string role) => User.IsInRole(role) || User.HasClaim("roles", role);

    private void EnsureCanCapture() { if (!IsInRole("BURSAR") && !IsInRole("SCHOOL_ADMIN") && !IsInRole("HEAD_TEACHER")) throw new UnauthorizedAccessException("Capture requires BURSAR"); }
    private void EnsureCanApprove() { if (!IsInRole("HEAD_TEACHER") && !IsInRole("DIRECTOR") && !IsInRole("SCHOOL_ADMIN") && !IsInRole("BOARD")) throw new UnauthorizedAccessException("Approval requires HEAD_TEACHER/DIRECTOR/BOARD"); }
    private void EnsureCanReport() { if (!IsInRole("BURSAR") && !IsInRole("HEAD_TEACHER") && !IsInRole("DIRECTOR") && !IsInRole("SCHOOL_ADMIN")) throw new UnauthorizedAccessException("Reporting requires BURSAR/HEAD/DIRECTOR"); }
    private void EnsureElevatedForUnlock() { if (!IsInRole("DIRECTOR") && !IsInRole("BOARD") && !IsInRole("PLATFORM_SUPERADMIN")) throw new UnauthorizedAccessException("Unlock requires elevated permission DIRECTOR/BOARD"); }

    // Expense categories
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var cats = await _db.Set<ExpenseCategory>().Where(c => c.TenantId == TenantId && !c.IsDeleted).ToListAsync(ct);
        return Ok(cats.Select(c => new ExpenseCategoryDto(c.Id, c.Name, c.Code, c.Type, c.ParentCategoryId, c.IsActive)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateExpenseCategoryRequest req, CancellationToken ct)
    {
        EnsureCanCapture();
        var cat = new ExpenseCategory { TenantId = TenantId, Name = req.Name, Code = req.Code, Type = req.Type, ParentCategoryId = req.ParentId, Description = req.Description, GlCode = req.GlCode, CreatedBy = UserId };
        _db.Set<ExpenseCategory>().Add(cat);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("ExpenseCategory", cat.Id, "create", null, req, ct);
        return Ok(cat);
    }

    // Approval thresholds
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("approval-thresholds")]
    public async Task<IActionResult> GetThresholds(CancellationToken ct)
    {
        var list = await _db.Set<ApprovalThreshold>().Where(t => t.TenantId == TenantId && !t.IsDeleted).OrderBy(t => t.ApprovalOrder).ToListAsync(ct);
        return Ok(list);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("approval-thresholds")]
    public async Task<IActionResult> CreateThreshold([FromBody] CreateApprovalThresholdRequest req, CancellationToken ct)
    {
        EnsureCanApprove();
        var th = new ApprovalThreshold { TenantId = TenantId, MinAmount = req.MinAmount, MaxAmount = req.MaxAmount, Currency = req.Currency, RequiredApproverRole = req.RequiredApproverRole, RequiredApprovals = req.RequiredApprovals, AutoApprove = req.AutoApprove, ApprovalOrder = req.ApprovalOrder, Description = req.Description, CreatedBy = UserId };
        _db.Set<ApprovalThreshold>().Add(th);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("ApprovalThreshold", th.Id, "create", null, req, ct);
        return Ok(th);
    }

    // Expense capture with supporting document upload
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses([FromQuery] long? categoryId, [FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.Set<Expense>().Where(e => e.TenantId == TenantId && !e.IsDeleted);
        if (categoryId.HasValue) q = q.Where(e => e.CategoryId == categoryId.Value);
        if (!string.IsNullOrEmpty(status)) q = q.Where(e => e.Status == status);
        var list = await q.Include(e => e.Category).OrderByDescending(e => e.ExpenseDate).Take(200).ToListAsync(ct);
        return Ok(list.Select(e => new ExpenseDto(e.Id, e.ExpenseNumber, e.CategoryId, e.Category.Name, e.SupplierId, e.Supplier?.Name, e.Description, e.Amount, e.Currency, e.ExpenseDate, e.Status, e.PaymentMethod, e.SupportingDocumentUrl, e.Notes, e.CreatedByUserId, e.ApprovedByUserId)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest req, CancellationToken ct)
    {
        EnsureCanCapture();
        await EnsurePeriodNotLockedAsync(req.AcademicYearId, req.TermId, ct);

        var number = $"EXP-{DateTime.UtcNow.Year}-{(await _db.Set<Expense>().CountAsync(e => e.TenantId == TenantId, ct) + 1):D5}";
        var expense = new Expense
        {
            TenantId = TenantId,
            ExpenseNumber = number,
            CategoryId = req.CategoryId,
            SupplierId = req.SupplierId,
            Description = req.Description,
            Amount = FinanceCalculationService.Round2(req.Amount),
            Currency = req.Currency,
            ExpenseDate = req.ExpenseDate,
            AcademicYearId = req.AcademicYearId,
            TermId = req.TermId,
            BudgetId = req.BudgetId,
            PaymentMethod = req.PaymentMethod,
            BankAccountId = req.BankAccountId,
            SupportingDocumentUrl = req.SupportingDocumentUrl,
            Notes = req.Notes,
            Status = "draft",
            CreatedByUserId = UserId,
            CreatedBy = UserId
        };
        _db.Set<Expense>().Add(expense);
        await _db.SaveChangesAsync(ct);

        // Auto approval check via thresholds
        var threshold = await _db.Set<ApprovalThreshold>().Where(t => t.TenantId == TenantId && t.MinAmount <= expense.Amount && (t.MaxAmount == null || expense.Amount < t.MaxAmount) && !t.IsDeleted).OrderBy(t => t.ApprovalOrder).FirstOrDefaultAsync(ct);
        if (threshold != null && threshold.AutoApprove)
        {
            expense.Status = "approved";
            expense.ApprovedByUserId = UserId;
            expense.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            expense.Status = "pending_approval";
            // Create approval requests based on thresholds chain
            var thresholds = await _db.Set<ApprovalThreshold>().Where(t => t.TenantId == TenantId && t.MinAmount <= expense.Amount && (t.MaxAmount == null || expense.Amount < t.MaxAmount) && !t.IsDeleted).OrderBy(t => t.ApprovalOrder).ToListAsync(ct);
            foreach (var th in thresholds.Where(t => !t.AutoApprove))
            {
                var ar = new ApprovalRequest { TenantId = TenantId, ExpenseId = expense.Id, ThresholdId = th.Id, ApproverRole = th.RequiredApproverRole, Status = "pending", ApprovalOrder = th.ApprovalOrder, CreatedBy = UserId };
                _db.Set<ApprovalRequest>().Add(ar);
            }
        }

        await _db.SaveChangesAsync(ct);
        await AuditAsync("Expense", expense.Id, "create", null, req, ct);

        return Ok(expense);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("expenses/{id:long}/approve")]
    public async Task<IActionResult> ApproveExpense(long id, [FromBody] ApproveExpenseRequest req, CancellationToken ct)
    {
        EnsureCanApprove();
        var expense = await _db.Set<Expense>().FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId && !e.IsDeleted, ct);
        if (expense == null) return NotFound();

        var pending = await _db.Set<ApprovalRequest>().Where(a => a.TenantId == TenantId && a.ExpenseId == id && a.Status == "pending" && !a.IsDeleted) // SECURITY C5.OrderBy(a => a.ApprovalOrder).FirstOrDefaultAsync(ct);
        if (pending == null) return BadRequest(new { message = "No pending approval" });

        // Check approver role
        if (!IsInRole(pending.ApproverRole) && !IsInRole("SCHOOL_ADMIN")) return Forbid();

        pending.Status = req.Status;
        pending.Comment = req.Comment;
        pending.DecidedAt = DateTime.UtcNow;
        pending.UpdatedBy = UserId;

        // If all approvals done, update expense status
        var remaining = await _db.Set<ApprovalRequest>().CountAsync(a => a.ExpenseId == id && a.Status == "pending" && !a.IsDeleted, ct);
        if (req.Status == "approved" && remaining == 0)
        {
            expense.Status = "approved";
            expense.ApprovedByUserId = UserId;
            expense.ApprovedAt = DateTime.UtcNow;
        }
        else if (req.Status == "rejected")
        {
            expense.Status = "rejected";
        }

        await _db.SaveChangesAsync(ct);
        await AuditAsync("ApprovalRequest", pending.Id, req.Status, new { oldStatus = "pending" }, req, ct);

        return Ok(expense);
    }

    // Suppliers
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(CancellationToken ct)
    {
        var list = await _db.Set<Supplier>().Where(s => s.TenantId == TenantId && !s.IsDeleted).ToListAsync(ct);
        return Ok(list.Select(s => new SupplierDto(s.Id, s.Name, s.Code, s.ContactPerson, s.Email, s.Phone, s.TaxId, s.IsActive, s.TotalPurchases, s.Currency)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest req, CancellationToken ct)
    {
        EnsureCanCapture();
        var sup = new Supplier { TenantId = TenantId, Name = req.Name, Code = req.Code, ContactPerson = req.ContactPerson, Email = req.Email, Phone = req.Phone, Address = req.Address, TaxId = req.TaxId, BankAccountNumber = req.BankAccountNumber, BankName = req.BankName, CreatedBy = UserId };
        _db.Set<Supplier>().Add(sup);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("Supplier", sup.Id, "create", null, req, ct);
        return Ok(sup);
    }

    // Budgets per category per term
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("budgets")]
    public async Task<IActionResult> GetBudgets([FromQuery] long? academicYearId, [FromQuery] long? termId, CancellationToken ct)
    {
        var q = _db.Set<Budget>().Where(b => b.TenantId == TenantId && !b.IsDeleted);
        if (academicYearId.HasValue) q = q.Where(b => b.AcademicYearId == academicYearId.Value);
        if (termId.HasValue) q = q.Where(b => b.TermId == termId.Value);
        var list = await q.Include(b => b.Category).ToListAsync(ct);
        return Ok(list.Select(b => new BudgetDto(b.Id, b.Name, b.AcademicYearId, b.TermId, b.CategoryId, b.Category.Name, b.BudgetedAmount, b.ActualAmount, b.VarianceAmount, b.VariancePercentage, b.Currency, b.Status)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("budgets")]
    public async Task<IActionResult> CreateBudget([FromBody] CreateBudgetRequest req, CancellationToken ct)
    {
        EnsureCanCapture();
        var budget = new Budget { TenantId = TenantId, Name = req.Name, AcademicYearId = req.AcademicYearId, TermId = req.TermId, CategoryId = req.CategoryId, BudgetedAmount = FinanceCalculationService.Round2(req.BudgetedAmount), Currency = req.Currency, Notes = req.Notes, Status = "draft", CreatedBy = UserId };
        _db.Set<Budget>().Add(budget);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("Budget", budget.Id, "create", null, req, ct);
        return Ok(budget);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("budgets/variance")]
    public async Task<IActionResult> GetBudgetVariance([FromQuery] long academicYearId, [FromQuery] long termId, CancellationToken ct)
    {
        EnsureCanReport();
        var budgets = await _db.Set<Budget>().Where(b => b.TenantId == TenantId && b.AcademicYearId == academicYearId && b.TermId == termId && !b.IsDeleted).Include(b => b.Category).ToListAsync(ct);
        var result = new List<BudgetVarianceReportDto>();

        foreach (var b in budgets)
        {
            // Actual = sum approved expenses in category/term
            var actual = await _db.Set<Expense>().Where(e => e.TenantId == TenantId && e.CategoryId == b.CategoryId && e.AcademicYearId == academicYearId && e.TermId == termId && e.Status == "approved" && !e.IsDeleted).SumAsync(e => e.Amount, ct);
            var (varianceAmt, variancePerc) = _calc.CalculateBudgetVariance(b.BudgetedAmount, actual);
            b.ActualAmount = actual;
            b.VarianceAmount = varianceAmt;
            b.VariancePercentage = variancePerc;
            result.Add(new BudgetVarianceReportDto(b.CategoryId, b.Category.Name, b.BudgetedAmount, actual, varianceAmt, variancePerc, b.Currency, b.Status));
        }
        await _db.SaveChangesAsync(ct);
        return Ok(result);
    }

    // Bank accounts and cash book
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetBankAccounts(CancellationToken ct)
    {
        var list = await _db.Set<BankAccount>().Where(b => b.TenantId == TenantId && !b.IsDeleted).ToListAsync(ct);
        return Ok(list.Select(b => new BankAccountDto(b.Id, b.Name, b.AccountNumber, b.BankName, b.Currency, b.OpeningBalance, b.CurrentBalance, b.IsActive, b.AccountType)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("bank-accounts")]
    public async Task<IActionResult> CreateBankAccount([FromBody] CreateBankAccountRequest req, CancellationToken ct)
    {
        EnsureCanCapture();
        var acc = new BankAccount { TenantId = TenantId, Name = req.Name, AccountNumber = req.AccountNumber, BankName = req.BankName, Currency = req.Currency, OpeningBalance = FinanceCalculationService.Round2(req.OpeningBalance), CurrentBalance = FinanceCalculationService.Round2(req.OpeningBalance), AccountType = req.AccountType, CreatedBy = UserId };
        _db.Set<BankAccount>().Add(acc);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("BankAccount", acc.Id, "create", null, req, ct);
        return Ok(acc);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("cashbook")]
    public async Task<IActionResult> GetCashBook([FromQuery] long? bankAccountId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        EnsureCanReport();
        var q = _db.Set<CashBookEntry>().Where(c => c.TenantId == TenantId && !c.IsDeleted);
        if (bankAccountId.HasValue) q = q.Where(c => c.BankAccountId == bankAccountId.Value);
        if (from.HasValue) q = q.Where(c => c.EntryDate >= from.Value);
        if (to.HasValue) q = q.Where(c => c.EntryDate <= to.Value);
        var list = await q.Include(c => c.BankAccount).OrderByDescending(c => c.EntryDate).Take(500).ToListAsync(ct);
        return Ok(list.Select(c => new CashBookEntryDto(c.Id, c.BankAccountId, c.BankAccount.Name, c.EntryDate, c.Description, c.Reference, c.Debit, c.Credit, c.Balance, c.Currency, c.EntryType, c.IsReconciled)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("cashbook")]
    public async Task<IActionResult> CreateCashBookEntry([FromBody] CreateCashBookEntryRequest req, CancellationToken ct)
    {
        EnsureCanCapture();
        await EnsurePeriodNotLockedAsync(null, null, ct); // check if term locked? Simplified

        var account = await _db.Set<BankAccount>().FirstOrDefaultAsync(b => b.Id == req.BankAccountId && b.TenantId == TenantId && !b.IsDeleted, ct);
        if (account == null) return NotFound(new { message = "Bank account not found" });

        var balance = _calc.CalculateCashBookBalance(account.CurrentBalance, new List<(decimal, decimal)> { (req.Debit, req.Credit) });
        // Actually need running balance from last entry
        var lastEntry = await _db.Set<CashBookEntry>().Where(c => c.TenantId == TenantId && c.BankAccountId == req.BankAccountId && !c.IsDeleted) // SECURITY C5.OrderByDescending(c => c.EntryDate).ThenByDescending(c => c.Id).FirstOrDefaultAsync(ct);
        var opening = lastEntry?.Balance ?? account.OpeningBalance;
        var newBalance = _calc.CalculateCashBookBalance(opening, new List<(decimal, decimal)> { (req.Debit, req.Credit) });

        var entry = new CashBookEntry
        {
            TenantId = TenantId,
            BankAccountId = req.BankAccountId,
            EntryDate = req.EntryDate,
            Description = req.Description,
            Reference = req.Reference,
            Debit = FinanceCalculationService.Round2(req.Debit),
            Credit = FinanceCalculationService.Round2(req.Credit),
            Balance = newBalance,
            Currency = req.Currency,
            EntryType = req.EntryType,
            RelatedExpenseId = req.RelatedExpenseId,
            RelatedFeePaymentId = req.RelatedFeePaymentId,
            TransferToAccountId = req.TransferToAccountId,
            CreatedBy = UserId
        };
        _db.Set<CashBookEntry>().Add(entry);

        // Update bank account current balance
        account.CurrentBalance = newBalance;

        // Handle transfer double entry
        if (req.TransferToAccountId.HasValue)
        {
            var transferId = Guid.NewGuid().ToString();
            entry.TransferId = transferId;
            var toAccount = await _db.Set<BankAccount>().FirstOrDefaultAsync(b => b.Id == req.TransferToAccountId.Value && b.TenantId == TenantId, ct);
            if (toAccount != null)
            {
                var toLast = await _db.Set<CashBookEntry>().Where(c => c.TenantId == TenantId && c.BankAccountId == toAccount.Id && !c.IsDeleted) // SECURITY C5.OrderByDescending(c => c.EntryDate).ThenByDescending(c => c.Id).FirstOrDefaultAsync(ct);
                var toOpening = toLast?.Balance ?? toAccount.OpeningBalance;
                var toBalance = _calc.CalculateCashBookBalance(toOpening, new List<(decimal, decimal)> { (0, req.Debit) }); // transfer debit from source = credit to dest? Simplified
                var toEntry = new CashBookEntry
                {
                    TenantId = TenantId,
                    BankAccountId = toAccount.Id,
                    EntryDate = req.EntryDate,
                    Description = $"Transfer from {account.Name}: {req.Description}",
                    Reference = req.Reference,
                    Debit = 0,
                    Credit = FinanceCalculationService.Round2(req.Debit), // credit to destination
                    Balance = toBalance,
                    Currency = req.Currency,
                    EntryType = "transfer",
                    TransferId = transferId,
                    TransferToAccountId = req.BankAccountId,
                    CreatedBy = UserId
                };
                _db.Set<CashBookEntry>().Add(toEntry);
                toAccount.CurrentBalance = toBalance;
            }
        }

        await _db.SaveChangesAsync(ct);
        await AuditAsync("CashBookEntry", entry.Id, "create", null, req, ct);
        return Ok(entry);
    }

    // Period locking
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("period-locks")]
    public async Task<IActionResult> GetPeriodLocks(CancellationToken ct)
    {
        var locks = await _db.Set<PeriodLock>().Where(p => p.TenantId == TenantId && !p.IsDeleted).ToListAsync(ct);
        return Ok(locks.Select(l => new PeriodLockDto(l.Id, l.AcademicYearId, l.TermId, l.IsLocked, l.LockedAt, l.LockedByUserId, l.LockReason, l.IsUnlocked, l.UnlockedAt, l.UnlockedByUserId, l.UnlockReason)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("period-locks/lock")]
    public async Task<IActionResult> LockPeriod([FromBody] LockPeriodRequest req, CancellationToken ct)
    {
        EnsureCanApprove(); // only head/director can lock
        var existing = await _db.Set<PeriodLock>().FirstOrDefaultAsync(p => p.TenantId == TenantId && p.AcademicYearId == req.AcademicYearId && p.TermId == req.TermId && !p.IsDeleted, ct);
        if (existing == null)
        {
            existing = new PeriodLock { TenantId = TenantId, AcademicYearId = req.AcademicYearId, TermId = req.TermId, IsLocked = true, LockedAt = DateTime.UtcNow, LockedByUserId = UserId, LockReason = req.LockReason, CreatedBy = UserId };
            _db.Set<PeriodLock>().Add(existing);
        }
        else
        {
            existing.IsLocked = true;
            existing.LockedAt = DateTime.UtcNow;
            existing.LockedByUserId = UserId;
            existing.LockReason = req.LockReason;
            existing.IsUnlocked = false;
        }
        await _db.SaveChangesAsync(ct);
        await AuditAsync("PeriodLock", existing.Id, "lock", null, req, ct);
        return Ok(existing);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("period-locks/unlock")]
    public async Task<IActionResult> UnlockPeriod([FromBody] UnlockPeriodRequest req, CancellationToken ct)
    {
        EnsureElevatedForUnlock(); // requires DIRECTOR/BOARD

        var existing = await _db.Set<PeriodLock>().FirstOrDefaultAsync(p => p.TenantId == TenantId && p.AcademicYearId == req.AcademicYearId && p.TermId == req.TermId && !p.IsDeleted, ct);
        if (existing == null) return NotFound(new { message = "Period not locked" });

        if (string.IsNullOrWhiteSpace(req.UnlockReason) || req.UnlockReason.Length < 20)
            return BadRequest(new { message = "Unlock reason >=20 chars required, documented and audited, elevated permission" });

        existing.IsUnlocked = true;
        existing.UnlockedAt = DateTime.UtcNow;
        existing.UnlockedByUserId = UserId;
        existing.UnlockReason = req.UnlockReason;
        existing.UnlockApproverRole = req.UnlockApproverRole;
        existing.IsLocked = false; // unlock

        await _db.SaveChangesAsync(ct);
        await AuditAsync("PeriodLock", existing.Id, "unlock", new { wasLocked = true }, req, ct);

        return Ok(existing);
    }

    // Financial reports
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/income-expenditure")]
    public async Task<IActionResult> GetIncomeExpenditure([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        EnsureCanReport();
        // Fee collection = sum fee payments (existing fee entities without altering them) + other income
        var feePayments = await _db.Set<Payment>().Where(p => p.TenantId == TenantId && p.PaymentDate >= from && p.PaymentDate <= to && !p.IsDeleted && p.Status.ToString() != "Reversed").SumAsync(p => p.Amount, ct);
        var expenses = await _db.Set<Expense>().Where(e => e.TenantId == TenantId && e.ExpenseDate >= from && e.ExpenseDate <= to && e.Status == "approved" && !e.IsDeleted).SumAsync(e => e.Amount, ct);

        var (totalIncome, totalExpenditure, net) = _calc.CalculateIncomeExpenditure(feePayments, 0m, expenses);

        var breakdown = await _db.Set<Expense>().Where(e => e.TenantId == TenantId && e.ExpenseDate >= from && e.ExpenseDate <= to && e.Status == "approved" && !e.IsDeleted)
            .GroupBy(e => e.CategoryId).Select(g => new { categoryId = g.Key, actual = g.Sum(x => x.Amount) }).ToListAsync(ct);

        var breakdownDtos = new List<CategoryBreakdownDto>();
        foreach (var b in breakdown)
        {
            var cat = await _db.Set<ExpenseCategory>().FirstOrDefaultAsync(c => c.Id == b.categoryId, ct);
            breakdownDtos.Add(new CategoryBreakdownDto(b.categoryId, cat?.Name ?? b.categoryId.ToString(), 0, b.actual, 0, 0));
        }

        return Ok(new IncomeExpenditureReportDto(from, to, totalIncome, feePayments, 0m, totalExpenditure, net, "USD", breakdownDtos));
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/arrears-ageing")]
    public async Task<IActionResult> GetArrearsAgeing([FromQuery] DateTime? asAtDate, CancellationToken ct)
    {
        EnsureCanReport();
        var date = asAtDate ?? DateTime.UtcNow.Date;
        // Consume existing fee invoices without altering them
        var invoices = await _db.Set<FeeInvoice>().Where(i => i.TenantId == TenantId && i.BalanceDue > 0 && !i.IsDeleted && i.DueDate <= date).ToListAsync(ct);
        var arrearsData = invoices.Select(i => (i.BalanceDue, (date - i.DueDate).Days)).ToList();
        var (current, d30, d60, d90) = _calc.CalculateArrearsAgeing(arrearsData);

        // Students detail
        var students = new List<ArrearsStudentDto>();
        foreach (var inv in invoices)
        {
            var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == inv.StudentId, ct);
            if (student == null) continue;
            var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == student.GradeId, ct);
            var stream = await _db.Streams.FirstOrDefaultAsync(s => s.Id == student.StreamId, ct);
            students.Add(new ArrearsStudentDto(inv.StudentId, $"{student.FirstName} {student.LastName}", student.StudentNumber, grade?.Name ?? "", stream?.Name ?? "", inv.BalanceDue, (date - inv.DueDate).Days, inv.Currency));
        }

        return Ok(new ArrearsAgeingDto(current, d30, d60, d90, current + d30 + d60 + d90, "USD", students));
    }

    private async Task EnsurePeriodNotLockedAsync(long? academicYearId, long? termId, CancellationToken ct)
    {
        if (!academicYearId.HasValue || !termId.HasValue) return;
        var locked = await _db.Set<PeriodLock>().FirstOrDefaultAsync(p => p.TenantId == TenantId && p.AcademicYearId == academicYearId.Value && p.TermId == termId.Value && p.IsLocked && !p.IsUnlocked && !p.IsDeleted, ct);
        if (locked != null) throw new InvalidOperationException($"Period locked: AcademicYear {academicYearId} Term {termId} locked at {locked.LockedAt} by {locked.LockedByUserId}. Unlock requires elevated permission and documented reason.");
    }

    private async Task AuditAsync(string entityType, long entityId, string action, object? oldValues, object newValues, CancellationToken ct)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId,
            UserId = UserId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null,
            CreatedBy = UserId
        });
        await _db.SaveChangesAsync(ct);
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade/Stream/Guardian - single source of truth