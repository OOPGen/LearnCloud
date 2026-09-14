using LearnCloud.Fees.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Fees.Services;

public interface IArrearsService
{
    Task<decimal> GetArrearsAsAtAsync(long tenantId, long studentId, DateTime asAtDate, CancellationToken ct = default);
    Task<List<ArrearsByClassDto>> GetArrearsByClassAsync(long tenantId, long academicYearId, long termId, DateTime asAtDate, CancellationToken ct = default);
    Task<List<ArrearsByAmountDto>> GetArrearsByAmountAsync(long tenantId, long academicYearId, long termId, DateTime asAtDate, string sortByAmount, CancellationToken ct = default);
    Task<LearnerStatementDto> GetLearnerStatementAsync(long tenantId, long studentId, DateTime? from, DateTime? to, CancellationToken ct = default);
}

public record ArrearsByClassDto(long GradeId, string GradeName, long StreamId, string StreamName, long StudentId, string StudentName, string StudentNumber, decimal BalanceDue, string Currency, int DaysOverdue, string GuardianBillingPhone, string InvoiceNumber);
public record ArrearsByAmountDto(long StudentId, string StudentName, string StudentNumber, long GradeId, string GradeName, long StreamId, string StreamName, decimal TotalArrears, string Currency);
public record LearnerStatementDto(long StudentId, string StudentName, string StudentNumber, List<StatementLineDto> Lines, decimal TotalInvoiced, decimal TotalPaid, decimal BalanceDue, decimal Credit);
public record StatementLineDto(DateTime Date, string Type, string Number, string Description, decimal Debit, decimal Credit, decimal Balance, string Currency);

public class ArrearsService : IArrearsService
{
    private readonly LearnCloudDbContext _db;
    private readonly FeeCalculationService _calc;

    public ArrearsService(LearnCloudDbContext db, FeeCalculationService calc) { _db = db; _calc = calc; }

    public async Task<decimal> GetArrearsAsAtAsync(long tenantId, long studentId, DateTime asAtDate, CancellationToken ct = default)
    {
        var invoices = await _db.Set<FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == studentId && !i.IsDeleted && i.IssueDate.Date <= asAtDate.Date).ToListAsync(ct);
        var invoiceForArrears = invoices.Select(i => new InvoiceForArrears(i.Id, i.TotalAmount, i.IssueDate, i.DueDate)).ToList();

        var allocations = await _db.Set<PaymentAllocation>().Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Join(_db.Set<Payment>().Where(p => p.TenantId == tenantId && p.StudentId == studentId && p.PaymentDate.Date <= asAtDate.Date && p.Status == PaymentStatus.Confirmed),
                alloc => alloc.PaymentId, pay => pay.Id, (alloc, pay) => new PaymentAllocationForArrears(alloc.InvoiceId, alloc.AllocatedAmount, pay.PaymentDate))
            .ToListAsync(ct);

        return _calc.ComputeOverdueArrearsAsAt(invoiceForArrears, allocations, asAtDate);
    }

    public async Task<List<ArrearsByClassDto>> GetArrearsByClassAsync(long tenantId, long academicYearId, long termId, DateTime asAtDate, CancellationToken ct = default)
    {
        // PERFORMANCE FIX H1: Was N+1 - loaded invoices then per invoice student, grade, stream via FirstOrDefault in loop
        // Fixed: Batch loading + single queries

        var invoices = await _db.Set<FeeInvoice>()
            .Where(i => i.TenantId == tenantId && i.AcademicYearId == academicYearId && i.TermId == termId && !i.IsDeleted && i.BalanceDue > 0 && i.DueDate.Date <= asAtDate.Date && i.Status != InvoiceStatus.Void)
            .AsNoTracking()
            .ToListAsync(ct);

        if (invoices.Count == 0) return new List<ArrearsByClassDto>();

        // Batch load students
        var studentIds = invoices.Select(i => i.StudentId).Distinct().ToList();
        var studentsDict = await _db.Set<Student>()
            .Where(s => s.TenantId == tenantId && studentIds.Contains(s.Id) && !s.IsDeleted)
            .ToDictionaryAsync(s => s.Id, ct);

        // Batch load grades and streams from students
        var gradeIds = studentsDict.Values.Select(s => s.GradeId).Distinct().ToList();
        var streamIds = studentsDict.Values.Select(s => s.StreamId).Distinct().ToList();

        var gradesDict = await _db.Set<Grade>().Where(g => gradeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, ct);
        var streamsDict = await _db.Set<ClassStream>().Where(s => streamIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        // Batch load billing guardian phone via guardian_student_links is_billing
        var billingLinks = await _db.Set<GuardianStudentLink>()
            .Where(l => l.TenantId == tenantId && studentIds.Contains(l.StudentId) && l.IsBillingContact && !l.IsDeleted)
            .ToListAsync(ct);
        var guardianIds = billingLinks.Select(l => l.GuardianId).Distinct().ToList();
        var guardiansDict = await _db.Set<Guardian>().Where(g => guardianIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, ct);

        var result = new List<ArrearsByClassDto>(invoices.Count);
        foreach (var inv in invoices)
        {
            studentsDict.TryGetValue(inv.StudentId, out var student);
            Grade? grade = null;
            ClassStream? stream = null;
            if (student != null)
            {
                gradesDict.TryGetValue(student.GradeId, out grade);
                streamsDict.TryGetValue(student.StreamId, out stream);
            }
            
            var billingLink = billingLinks.FirstOrDefault(l => l.StudentId == inv.StudentId);
            string guardianPhone = "";
            if (billingLink != null && guardiansDict.TryGetValue(billingLink.GuardianId, out var guardian))
            {
                guardianPhone = guardian.Phone;
            }

            var daysOverdue = (asAtDate.Date - inv.DueDate.Date).Days;

            result.Add(new ArrearsByClassDto(
                student?.GradeId ?? 0,
                grade?.Name ?? "",
                student?.StreamId ?? 0,
                stream?.Name ?? "",
                inv.StudentId,
                student != null ? $"{student.FirstName} {student.LastName}" : $"Student {inv.StudentId}",
                student?.StudentNumber ?? "",
                inv.BalanceDue,
                inv.Currency,
                daysOverdue,
                guardianPhone,
                inv.InvoiceNumber
            ));
        }

        return result.OrderByDescending(r => r.BalanceDue).ThenByDescending(r => r.DaysOverdue).ToList();
    }

    public async Task<List<ArrearsByAmountDto>> GetArrearsByAmountAsync(long tenantId, long academicYearId, long termId, DateTime asAtDate, string sortByAmount, CancellationToken ct = default)
    {
        var byClass = await GetArrearsByClassAsync(tenantId, academicYearId, termId, asAtDate, ct);
        var grouped = byClass.GroupBy(r => r.StudentId)
            .Select(g => new ArrearsByAmountDto(
                g.Key,
                g.First().StudentName,
                g.First().StudentNumber,
                g.First().GradeId,
                g.First().GradeName,
                g.First().StreamId,
                g.First().StreamName,
                FeeCalculationService.Round2(g.Sum(x => x.BalanceDue)),
                g.First().Currency
            )).ToList();

        return sortByAmount == "asc" ? grouped.OrderBy(g => g.TotalArrears).ToList() : grouped.OrderByDescending(g => g.TotalArrears).ToList();
    }

    public async Task<LearnerStatementDto> GetLearnerStatementAsync(long tenantId, long studentId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId, ct) ?? throw new InvalidOperationException("Student not found");
        var invoices = await _db.Set<FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == studentId && !i.IsDeleted).OrderBy(i => i.IssueDate).ToListAsync(ct);
        var payments = await _db.Set<Payment>().Where(p => p.TenantId == tenantId && p.StudentId == studentId && !p.IsDeleted && p.Status != PaymentStatus.Reversed).OrderBy(p => p.PaymentDate).ToListAsync(ct);
        var credits = await _db.Set<LearnerCredit>().Where(c => c.TenantId == tenantId && c.StudentId == studentId && !c.IsDeleted).ToListAsync(ct);

        var lines = new List<StatementLineDto>();
        decimal runningBalance = 0m;

        foreach (var inv in invoices)
        {
            if (from.HasValue && inv.IssueDate < from.Value) continue;
            if (to.HasValue && inv.IssueDate > to.Value) continue;
            runningBalance = FeeCalculationService.Round2(runningBalance + inv.TotalAmount);
            lines.Add(new StatementLineDto(inv.IssueDate, "Invoice", inv.InvoiceNumber, $"Invoice {inv.InvoiceNumber} - {inv.Status}", inv.TotalAmount, 0m, runningBalance, inv.Currency));
        }

        foreach (var pay in payments)
        {
            if (from.HasValue && pay.PaymentDate < from.Value) continue;
            if (to.HasValue && pay.PaymentDate > to.Value) continue;
            runningBalance = FeeCalculationService.Round2(runningBalance - pay.Amount);
            lines.Add(new StatementLineDto(pay.PaymentDate, "Payment", pay.ReceiptNumber, $"Payment {pay.Method} {pay.Reference}", 0m, pay.Amount, runningBalance, pay.Currency));
        }

        var totalInvoiced = FeeCalculationService.Round2(invoices.Sum(i => i.TotalAmount));
        var totalPaid = FeeCalculationService.Round2(payments.Sum(p => p.Amount));
        var credit = FeeCalculationService.Round2(credits.Where(c => !c.IsUtilized).Sum(c => c.Amount));
        var balance = FeeCalculationService.Round2(totalInvoiced - totalPaid);

        lines = lines.OrderBy(l => l.Date).ToList();

        return new LearnerStatementDto(studentId, $"{student.FirstName} {student.LastName}", student.StudentNumber, lines, totalInvoiced, totalPaid, balance, credit);
    }
}

// Stub entities for compilation

// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Final cleanup - single source of truth
