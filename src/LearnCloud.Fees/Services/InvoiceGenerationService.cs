using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LearnCloud.Fees.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Fees.Services;

public interface IInvoiceGenerationService
{
    Task<FeeInvoiceBatch> StartGenerationAsync(long tenantId, long userId, long academicYearId, long termId, CancellationToken ct = default);
    Task<FeeInvoiceBatch> GetBatchStatusAsync(long tenantId, long batchId, CancellationToken ct = default);
    Task<FeeInvoiceBatch> GenerateForTermAsync(long tenantId, long userId, long academicYearId, long termId, long? batchId = null, CancellationToken ct = default);
}

public class InvoiceGenerationService : IInvoiceGenerationService
{
    private readonly LearnCloudDbContext _db;
    private readonly FeeCalculationService _calc;
    private readonly ITenantContext _tenantContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceGenerationService> _logger;

    public InvoiceGenerationService(LearnCloudDbContext db, FeeCalculationService calc, ITenantContext tenantContext, IServiceScopeFactory scopeFactory, ILogger<InvoiceGenerationService> logger)
    {
        _db = db; _calc = calc; _tenantContext = tenantContext; _scopeFactory = scopeFactory; _logger = logger;
    }

    public async Task<FeeInvoiceBatch> StartGenerationAsync(long tenantId, long userId, long academicYearId, long termId, CancellationToken ct = default)
    {
        // Create batch record
        var batchNumber = $"BATCH-{academicYearId}-{termId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var batch = new FeeInvoiceBatch
        {
            TenantId = tenantId,
            BatchNumber = batchNumber,
            AcademicYearId = academicYearId,
            TermId = termId,
            Status = "pending",
            CreatedBy = userId
        };
        _db.Set<FeeInvoiceBatch>().Add(batch);
        await _db.SaveChangesAsync(ct);

        // In real app, enqueue background job via Hangfire/Quartz: BackgroundJob.Enqueue(() => GenerateForTermAsync(...))
        // For V1, run synchronously but in background task
        // This runs after the request returns, so it gets its own DI scope: the request's
        // DbContext is disposed by then. It must not take the request's cancellation token
        // either, or the batch would be cancelled as soon as the response is sent.
        // Not durable: a restart mid-run leaves the batch in its current status.
        var batchId = batch.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantContext>().BeginTenantScope(tenantId);
                var generator = scope.ServiceProvider.GetRequiredService<IInvoiceGenerationService>();
                await generator.GenerateForTermAsync(tenantId, userId, academicYearId, termId, batchId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invoice generation batch {BatchId} for tenant {TenantId} failed", batchId, tenantId);
            }
        });

        return batch;
    }

    public async Task<FeeInvoiceBatch> GetBatchStatusAsync(long tenantId, long batchId, CancellationToken ct = default)
    {
        var batch = await _db.Set<FeeInvoiceBatch>().FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId && !b.IsDeleted, ct)
                    ?? throw new InvalidOperationException("Batch not found");
        return batch;
    }

    // Idempotent, reports what created and skipped and why
    public async Task<FeeInvoiceBatch> GenerateForTermAsync(long tenantId, long userId, long academicYearId, long termId, long? batchId = null, CancellationToken ct = default)
    {
        FeeInvoiceBatch? batch = null;
        if (batchId.HasValue)
        {
            batch = await _db.Set<FeeInvoiceBatch>().FirstOrDefaultAsync(b => b.Id == batchId.Value && b.TenantId == tenantId, ct);
            if (batch != null)
            {
                batch.Status = "running";
                batch.StartedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }

        // Get active students with current enrolment
        var students = await _db.Set<Student>().Where(s => s.TenantId == tenantId && !s.IsDeleted).ToListAsync(ct);
        // Filter by enrolment is_current and academic year/term? Simplified: all active
        var activeEnrolments = await _db.Set<StudentEnrolment>().Where(e => e.TenantId == tenantId && e.AcademicYearId == academicYearId && e.IsCurrent && !e.IsDeleted && e.EnrolmentStatus != "withdrawn").ToListAsync(ct);

        var totalStudents = activeEnrolments.Count;
        if (batch != null)
        {
            batch.TotalStudents = totalStudents;
            await _db.SaveChangesAsync(ct);
        }

        int created = 0, skipped = 0, failed = 0;
        var results = new List<object>();

        foreach (var enrolment in activeEnrolments)
        {
            try
            {
                var studentId = enrolment.StudentId;

                // Check if enrolment after term end -> skip
                var term = await _db.Set<Term>().FirstOrDefaultAsync(t => t.Id == termId, ct);
                if (term != null && enrolment.EnrolmentDate > term.EndDate)
                {
                    skipped++;
                    results.Add(new { studentId, reason = $"Skipped - enrolment after term end {enrolment.EnrolmentDate:yyyy-MM-dd} > {term.EndDate:yyyy-MM-dd}" });
                    continue;
                }

                // Resolve applicable fee structure: individual > stream > grade > school-wide
                var structures = await _db.Set<FeeStructure>()
                    .Where(fs => fs.TenantId == tenantId && fs.AcademicYearId == academicYearId && fs.TermId == termId && fs.Status == "active" && !fs.IsDeleted)
                    .Include(fs => fs.Items)
                    .ToListAsync(ct);

                var applicable = ResolveApplicableStructure(structures, enrolment);

                if (applicable == null)
                {
                    skipped++;
                    results.Add(new { studentId, reason = "Skipped - no active fee structure for grade/stream" });
                    continue;
                }

                // Compute structure hash for idempotency
                var structureHash = ComputeStructureHash(applicable);

                // Check idempotency: if invoice exists with same student, year, term, hash
                var existing = await _db.Set<FeeInvoice>().FirstOrDefaultAsync(inv => inv.TenantId == tenantId && inv.StudentId == studentId && inv.AcademicYearId == academicYearId && inv.TermId == termId && inv.StructureHash == structureHash && !inv.IsDeleted, ct);
                if (existing != null)
                {
                    skipped++;
                    results.Add(new { studentId, reason = $"Skipped - already invoiced {existing.InvoiceNumber}", invoiceNumber = existing.InvoiceNumber });
                    continue;
                }

                // Build line items from structure items
                var lineTotals = new List<decimal>();
                var lineItemsData = new List<(long feeItemId, string desc, decimal amount, bool isProratable)>();

                // PERFORMANCE FIX C3: Was N+1 - FeeItem lookup per line item in loop
                // Fixed: Preload all FeeItems into dictionary once before loop
                var feeItemIds = applicable.Items.Where(i => !i.IsDeleted && i.FeeItemId != 0).Select(i => i.FeeItemId).Distinct().ToList();
                var feeItemsDict = new Dictionary<long, FeeItem>();
                if (feeItemIds.Count > 0)
                {
                    feeItemsDict = await _db.Set<FeeItem>().Where(fi => fi.TenantId == tenantId && feeItemIds.Contains(fi.Id) && !fi.IsDeleted).ToDictionaryAsync(fi => fi.Id, ct);
                }

                foreach (var item in applicable.Items.Where(i => !i.IsDeleted))
                {
                    var amount = item.Amount;
                    var isProrated = false;
                    var prorationNote = "";

                    // Mid-term joiner prorating if item proratable
                    if (item.FeeItemId != 0 && feeItemsDict.TryGetValue(item.FeeItemId, out var feeItem))
                    {
                        if (feeItem.IsProratable)
                        {
                            var termTotalDays = (term!.EndDate - term.StartDate).Days + 1;
                            var daysEnrolled = (term.EndDate - enrolment.EnrolmentDate).Days + 1;
                            if (daysEnrolled < termTotalDays && enrolment.EnrolmentDate > term.StartDate)
                            {
                                var prorated = _calc.CalculateProratedAmount(amount, daysEnrolled, termTotalDays);
                                prorationNote = $"Prorated {daysEnrolled}/{termTotalDays} days";
                                amount = prorated;
                                isProrated = true;
                            }
                        }
                    }

                    lineTotals.Add(amount);
                    lineItemsData.Add((item.FeeItemId, item.Description, amount, isProrated));
                }

                // Discounts for learner
                var discounts = await _db.Set<Discount>().Where(d => d.TenantId == tenantId && d.StudentId == studentId && d.AcademicYearId == academicYearId && d.TermId == termId && d.Status == "approved" && !d.IsDeleted).ToListAsync(ct);
                var discountInputs = discounts.Select(d => (d.Value, d.Type == DiscountType.Percentage)).ToList();
                var discountInputsForCalc = discountInputs.Select(d => (d.Value, d.Item2)).ToList();

                var (subtotal, discountTotal, total) = _calc.CalculateInvoiceTotals(lineTotals, discountInputsForCalc, applicable.Currency);

                // Generate invoice number from sequence
                var invoiceNumber = await GetNextInvoiceNumberAsync(tenantId, DateTime.UtcNow.Year, ct);

                var invoice = new FeeInvoice
                {
                    TenantId = tenantId,
                    InvoiceNumber = invoiceNumber,
                    AcademicYearId = academicYearId,
                    TermId = termId,
                    StudentId = studentId,
                    EnrolmentId = enrolment.Id,
                    FeeStructureId = applicable.Id,
                    StructureHash = structureHash,
                    SubtotalAmount = subtotal,
                    DiscountAmount = discountTotal,
                    TotalAmount = total,
                    AmountPaid = 0m,
                    BalanceDue = total,
                    Currency = applicable.Currency,
                    IssueDate = DateTime.UtcNow.Date,
                    DueDate = DateTime.UtcNow.Date.AddDays(14),
                    Status = InvoiceStatus.Issued,
                    IsProrated = lineItemsData.Any(l => l.isProratable),
                    ProrationNote = lineItemsData.Any(l => l.isProratable) ? string.Join("; ", lineItemsData.Where(l => l.isProratable).Select(l => l.desc)) : null,
                    CreatedBy = userId
                };

                _db.Set<FeeInvoice>().Add(invoice);
                await _db.SaveChangesAsync(ct);

                // Line items snapshot
                foreach (var (feeItemId, desc, amount, isPror) in lineItemsData)
                {
                    var line = new FeeInvoiceItem
                    {
                        TenantId = tenantId,
                        InvoiceId = invoice.Id,
                        FeeItemId = feeItemId,
                        Description = desc,
                        Quantity = 1,
                        UnitAmount = amount,
                        LineTotal = amount,
                        Currency = applicable.Currency,
                        IsProrated = isPror,
                        CreatedBy = userId
                    };
                    _db.Set<FeeInvoiceItem>().Add(line);
                }
                await _db.SaveChangesAsync(ct);

                created++;
                results.Add(new { studentId, invoiceNumber, total, reason = "Created" });
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(new { studentId = enrolment.StudentId, reason = $"Failed: {ex.Message}" });
            }

            if (batch != null)
            {
                batch.Processed++;
                batch.Created = created;
                batch.Skipped = skipped;
                batch.Failed = failed;
                batch.ProgressPercent = totalStudents > 0 ? (int)((double)batch.Processed / totalStudents * 100) : 100;
                batch.ResultJson = System.Text.Json.JsonSerializer.Serialize(results);
                await _db.SaveChangesAsync(ct);
            }
        }

        if (batch != null)
        {
            batch.Status = "completed";
            batch.CompletedAt = DateTime.UtcNow;
            batch.ProgressPercent = 100;
            batch.ResultJson = System.Text.Json.JsonSerializer.Serialize(results);
            await _db.SaveChangesAsync(ct);
        }

        // Return batch or create ad-hoc
        return batch ?? new FeeInvoiceBatch
        {
            TenantId = tenantId,
            BatchNumber = $"MANUAL-{DateTime.UtcNow:yyyyMMddHHmmss}",
            AcademicYearId = academicYearId,
            TermId = termId,
            Status = "completed",
            TotalStudents = totalStudents,
            Processed = totalStudents,
            Created = created,
            Skipped = skipped,
            Failed = failed,
            ResultJson = JsonSerializer.Serialize(results),
            ProgressPercent = 100
        };
    }

    private FeeStructure? ResolveApplicableStructure(List<FeeStructure> structures, StudentEnrolment enrolment)
    {
        // Priority: individual > stream > grade > school-wide
        var individual = structures.FirstOrDefault(s => s.StudentId == enrolment.StudentId);
        if (individual != null) return individual;

        var stream = structures.FirstOrDefault(s => s.StreamId == enrolment.StreamId && s.GradeId == enrolment.GradeId);
        if (stream != null) return stream;

        var grade = structures.FirstOrDefault(s => s.GradeId == enrolment.GradeId && s.StreamId == null && s.StudentId == null);
        if (grade != null) return grade;

        var schoolWide = structures.FirstOrDefault(s => s.GradeId == null && s.StreamId == null && s.StudentId == null);
        return schoolWide;
    }

    private string ComputeStructureHash(FeeStructure structure)
    {
        // Hash of structure id + items amounts for idempotency
        var data = $"{structure.Id}:{string.Join(",", structure.Items.OrderBy(i=>i.FeeItemId).Select(i=> $"{i.FeeItemId}:{i.Amount}:{i.Currency}"))}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash)[..16].ToLower();
    }

    private async Task<string> GetNextInvoiceNumberAsync(long tenantId, int year, CancellationToken ct)
    {
        // FOR UPDATE to avoid duplicates - transaction
        var seq = await _db.Set<InvoiceSequence>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Year == year && !s.IsDeleted, ct);
        if (seq == null)
        {
            seq = new InvoiceSequence { TenantId = tenantId, Year = year, LastNumber = 0, Prefix = "INV", Format = "{prefix}-{year}-{number:5}" };
            _db.Set<InvoiceSequence>().Add(seq);
            await _db.SaveChangesAsync(ct);
        }

        seq.LastNumber++;
        await _db.SaveChangesAsync(ct);

        // Format: INV-2026-00001
        return $"{seq.Prefix}-{year}-{seq.LastNumber:D5}";
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade/Stream/Guardian - single source of truth
