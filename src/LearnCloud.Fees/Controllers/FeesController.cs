using LearnCloud.Fees.DTOs;
using LearnCloud.Fees.Entities;
using LearnCloud.Fees.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Fees.Controllers;

[ApiController]
[Route("api/fees")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class FeesController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly FeeCalculationService _calc;
    private readonly IInvoiceGenerationService _invoiceGen;
    private readonly IPaymentService _paymentService;
    private readonly IArrearsService _arrearsService;

    public FeesController(LearnCloudDbContext db, ITenantContext tenantContext, FeeCalculationService calc, IInvoiceGenerationService invoiceGen, IPaymentService paymentService, IArrearsService arrearsService)
    {
        _db = db; _tenantContext = tenantContext; _calc = calc; _invoiceGen = invoiceGen; _paymentService = paymentService; _arrearsService = arrearsService;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Fee Items - bursar can manage, head view
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("items")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN,HEAD_TEACHER")]
    [ProducesResponseType(typeof(List<FeeItem>), 200)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> GetFeeItems([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        // SECURITY FIX C4: Added pagination to prevent large payload DoS, was returning all items
        var q = _db.Set<FeeItem>().Where(f => f.TenantId == TenantId && !f.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(f => f.Name.ToLower().Contains(s) || f.Code.ToLower().Contains(s));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(f => f.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        
        Response.Headers["X-Total-Count"] = total.ToString();
        Response.Headers["X-Page"] = page.ToString();
        Response.Headers["X-Page-Size"] = pageSize.ToString();
        
        // For backward compat, return List but with headers. V2 should return PagedResult
        return Ok(items);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("items")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN")]
    [ProducesResponseType(typeof(FeeItem), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateFeeItem([FromBody] CreateFeeItemRequest req, CancellationToken ct)
    {
        // SECURITY FIX C3: Was binding entity FeeItem directly with TenantId client could set - now uses DTO without TenantId
        var entity = new FeeItem
        {
            TenantId = TenantId,
            Name = req.Name,
            Code = req.Code,
            Recurrence = Enum.TryParse<FeeItemRecurrence>(req.Recurrence, true, out var rec) ? rec : FeeItemRecurrence.PerTerm,
            IsProratable = req.IsProratable,
            IsOptional = req.IsOptional,
            GlCode = req.GlCode,
            Description = req.Description,
            CreatedBy = UserId
        };
        _db.Set<FeeItem>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetFeeItems), new { id = entity.Id }, entity);
    }

    // Fee Structures - bursar can invoice and receipt; head can view; teacher can see nothing
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("structures")]
    [Authorize(Policy = "RequireFeesStructuresRead")]
    [ProducesResponseType(typeof(List<FeeStructureDto>), 200)]
    public async Task<IActionResult> GetStructures([FromQuery] long academicYearId, [FromQuery] long termId, CancellationToken ct)
    {
        // PERFORMANCE FIX H2: Was Include ThenInclude over-fetching all navigation, now Select projection + AsNoTracking for read-only
        var structures = await _db.Set<FeeStructure>()
            .Where(s => s.TenantId == TenantId && s.AcademicYearId == academicYearId && s.TermId == termId && !s.IsDeleted)
            .AsNoTracking()
            .Select(s => new FeeStructureDto(
                s.Id,
                s.Name,
                s.AcademicYearId,
                s.TermId,
                s.GradeId,
                s.StreamId,
                s.StudentId,
                s.Status,
                s.Currency,
                s.Items.Where(i => !i.IsDeleted).Select(i => new FeeStructureItemDto(i.Id, i.FeeStructureId, i.FeeItemId, i.Description, i.Amount, i.Currency, i.Quantity, i.LineTotal)).ToList(),
                s.CreatedAt
            ))
            .ToListAsync(ct);
        return Ok(structures);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("structures")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN")]
    public async Task<IActionResult> CreateStructure([FromBody] CreateStructureRequest req, CancellationToken ct)
    {
        var structure = new FeeStructure
        {
            TenantId = TenantId,
            Name = req.Name,
            AcademicYearId = req.AcademicYearId,
            TermId = req.TermId,
            GradeId = req.GradeId,
            StreamId = req.StreamId,
            StudentId = req.StudentId,
            Status = "active",
            Currency = req.Currency,
            CreatedBy = UserId
        };
        _db.Set<FeeStructure>().Add(structure);
        await _db.SaveChangesAsync(ct);

        foreach (var item in req.Items)
        {
            var entity = new FeeStructureItem
            {
                TenantId = TenantId,
                FeeStructureId = structure.Id,
                FeeItemId = item.FeeItemId,
                Description = item.Description,
                Amount = item.Amount,
                Currency = item.Currency,
                Quantity = item.Quantity,
                LineTotal = FeeCalculationService.Round2(item.Amount * item.Quantity),
                CreatedBy = UserId
            };
            _db.Set<FeeStructureItem>().Add(entity);
        }
        await _db.SaveChangesAsync(ct);

        return Ok(structure);
    }

    // Invoice generation for whole term runs as background job, idempotent, reports what created and skipped and why
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("invoices/generate-term")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN")]
    public async Task<IActionResult> GenerateTermInvoices([FromBody] GenerateTermRequest req, CancellationToken ct)
    {
        var batch = await _invoiceGen.StartGenerationAsync(TenantId, UserId, req.AcademicYearId, req.TermId, ct);
        return Ok(batch);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("invoices/batches/{batchId:long}")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> GetBatchStatus(long batchId, CancellationToken ct)
    {
        var batch = await _invoiceGen.GetBatchStatusAsync(TenantId, batchId, ct);
        return Ok(batch);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("invoices")]
    [Authorize(Policy = "RequireFeesInvoicesRead")]
    [ProducesResponseType(typeof(List<FeeInvoice>), 200)]
    public async Task<IActionResult> GetInvoices([FromQuery] long? studentId, [FromQuery] long? academicYearId, [FromQuery] long? termId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? sortBy = "issueDate", [FromQuery] bool sortDesc = true, CancellationToken ct = default)
    {
        // SECURITY FIX C4: Was Take(100) truncating silently, no pagination, no total count - now proper pagination with headers
        var q = _db.Set<FeeInvoice>().Where(i => i.TenantId == TenantId && !i.IsDeleted);
        if (studentId.HasValue) q = q.Where(i => i.StudentId == studentId.Value);
        if (academicYearId.HasValue) q = q.Where(i => i.AcademicYearId == academicYearId.Value);
        if (termId.HasValue) q = q.Where(i => i.TermId == termId.Value);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<InvoiceStatus>(status, true, out var st)) q = q.Where(i => i.Status == st);

        var total = await q.CountAsync(ct);
        
        q = sortBy?.ToLower() switch
        {
            "duedate" => sortDesc ? q.OrderByDescending(i => i.DueDate) : q.OrderBy(i => i.DueDate),
            "total" => sortDesc ? q.OrderByDescending(i => i.TotalAmount) : q.OrderBy(i => i.TotalAmount),
            "balance" => sortDesc ? q.OrderByDescending(i => i.BalanceDue) : q.OrderBy(i => i.BalanceDue),
            _ => sortDesc ? q.OrderByDescending(i => i.IssueDate) : q.OrderBy(i => i.IssueDate)
        };

        var list = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        
        Response.Headers["X-Total-Count"] = total.ToString();
        Response.Headers["X-Page"] = page.ToString();
        Response.Headers["X-Page-Size"] = pageSize.ToString();
        Response.Headers["Link"] = $"<{Request.Path}?page={page+1}&pageSize={pageSize}>; rel=\"next\"";
        
        return Ok(list);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("invoices/{id:long}")]
    [Authorize(Policy = "RequireFeesInvoicesRead")]
    public async Task<IActionResult> GetInvoice(long id, CancellationToken ct)
    {
        var inv = await _db.Set<FeeInvoice>().Include(i => i.LineItems).FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId && !i.IsDeleted, ct);
        if (inv == null) return NotFound();
        return Ok(inv);
    }

    // Payments recorded against learner with method, reference, date and receipt number, then allocated
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("payments")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentRequestDto req, CancellationToken ct)
    {
        var paymentReq = new RecordPaymentRequest(req.StudentId, req.Amount, req.Currency, req.Method, req.Reference, req.PaymentDate, req.ProofUrl,
            req.ManualAllocations?.Select(m => new ManualAllocationDto(m.InvoiceId, m.Amount, m.Currency)).ToList());

        var payment = await _paymentService.RecordPaymentAsync(TenantId, UserId, paymentReq, ct);
        return Ok(payment);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("payments/{id:long}/reverse")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN")]
    public async Task<IActionResult> ReversePayment(long id, [FromBody] ReversePaymentRequest req, CancellationToken ct)
    {
        var reversed = await _paymentService.ReversePaymentAsync(TenantId, UserId, id, req.Reason, ct);
        return Ok(reversed);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("payments/{id:long}")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> GetPayment(long id, CancellationToken ct)
    {
        var pay = await _paymentService.GetPaymentAsync(TenantId, id, ct);
        return Ok(pay);
    }

    // Arrears computed from unpaid invoice balances as at a date
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("arrears/by-class")]
    [Authorize(Policy = "RequireFeesReportsRead")]
    public async Task<IActionResult> GetArrearsByClass([FromQuery] long academicYearId, [FromQuery] long termId, [FromQuery] DateTime? asAtDate, CancellationToken ct)
    {
        var date = asAtDate ?? DateTime.UtcNow.Date;
        var result = await _arrearsService.GetArrearsByClassAsync(TenantId, academicYearId, termId, date, ct);
        return Ok(result);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("arrears/by-amount")]
    [Authorize(Policy = "RequireFeesReportsRead")]
    public async Task<IActionResult> GetArrearsByAmount([FromQuery] long academicYearId, [FromQuery] long termId, [FromQuery] DateTime? asAtDate, [FromQuery] string sort = "desc", CancellationToken ct = default)
    {
        var date = asAtDate ?? DateTime.UtcNow.Date;
        var result = await _arrearsService.GetArrearsByAmountAsync(TenantId, academicYearId, termId, date, sort, ct);
        return Ok(result);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("statements/{studentId:long}")]
    [Authorize(Policy = "RequireFeesInvoicesRead")]
    public async Task<IActionResult> GetStatement(long studentId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var stmt = await _arrearsService.GetLearnerStatementAsync(TenantId, studentId, from, to, ct);
        return Ok(stmt);
    }

    // Printable and downloadable: invoice, receipt, learner statement, arrears list by class and by amount owed
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("invoices/{id:long}/print")]
    [Authorize(Policy = "RequireFeesInvoicesRead")]
    public async Task<IActionResult> PrintInvoice(long id, CancellationToken ct)
    {
        var inv = await _db.Set<FeeInvoice>().Include(i => i.LineItems).FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId, ct);
        if (inv == null) return NotFound();
        var html = $"<html><head><style>body{{font-family:sans-serif}} table{{border-collapse:collapse;width:100%}} th,td{{border:1px solid #000;padding:6px}} .header{{background:#0F153A;color:white;padding:12px}}</style></head><body><div class='header'><h1>Invoice {inv.InvoiceNumber}</h1><p>Primary color #0F153A for printed docs - greyscale legible</p></div><p>Student {inv.StudentId} | Issue {inv.IssueDate:dd/MM/yyyy} | Due {inv.DueDate:dd/MM/yyyy} | Currency {inv.Currency}</p><table><tr><th>Description</th><th>Amount</th></tr>{string.Join("", inv.LineItems.Select(li => $"<tr><td>{li.Description}</td><td>{li.LineTotal} {li.Currency}</td></tr>"))}</table><p>Subtotal {inv.SubtotalAmount} Discount {inv.DiscountAmount} Total {inv.TotalAmount} Paid {inv.AmountPaid} Balance {inv.BalanceDue}</p></body></html>";
        return Content(html, "text/html");
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("payments/{id:long}/receipt/print")]
    [Authorize(Policy = "RequireFeesInvoicesRead")] // SECURITY FIX C2: Was missing [Authorize] - allowed anonymous financial data leak
    public async Task<IActionResult> PrintReceipt(long id, CancellationToken ct)
    {
        var pay = await _db.Set<Payment>().FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pay == null) return NotFound();
        var html = $"<html><body><h1>Receipt {pay.ReceiptNumber}</h1><p>Student {pay.StudentId} Amount {pay.Amount} {pay.Currency} Method {pay.Method} Date {pay.PaymentDate:dd/MM/yyyy} Ref {pay.Reference}</p><p>Printed from LearnCloud Bulawayo</p></body></html>";
        return Content(html, "text/html");
    }

    // Credit notes
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("credit-notes")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN")]
    public async Task<IActionResult> CreateCreditNote([FromBody] CreateCreditNoteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10) return BadRequest(new { message = "Reason >=10 chars required" });

        var invoice = await _db.Set<FeeInvoice>().FirstOrDefaultAsync(i => i.Id == req.InvoiceId && i.TenantId == TenantId, ct);
        if (invoice == null) return NotFound();

        var cnNumber = $"CN-{DateTime.UtcNow.Year}-{(await _db.Set<CreditNote>().CountAsync(c=>c.TenantId==TenantId, ct)+1):D5}";

        var cn = new CreditNote
        {
            TenantId = TenantId,
            InvoiceId = req.InvoiceId,
            StudentId = invoice.StudentId,
            Amount = FeeCalculationService.Round2(req.Amount),
            Currency = req.Currency,
            Reason = req.Reason,
            ApproverUserId = UserId,
            CreditNoteNumber = cnNumber,
            CreatedBy = UserId
        };
        _db.Set<CreditNote>().Add(cn);

        // Reduce invoice balance
        invoice.BalanceDue = FeeCalculationService.Round2(invoice.BalanceDue - cn.Amount);
        if (invoice.BalanceDue < 0) invoice.BalanceDue = 0m;
        invoice.AmountPaid = FeeCalculationService.Round2(invoice.TotalAmount - invoice.BalanceDue);

        // Audit
        _db.AuditLogs.Add(new LearnCloud.MultiTenancy.Entities.AuditLog
        {
            TenantId = TenantId,
            UserId = UserId,
            EntityType = "CreditNote",
            EntityId = cn.Id,
            Action = "create",
            NewValues = $"{{\"invoiceId\":{cn.InvoiceId},\"amount\":{cn.Amount},\"reason\":\"{cn.Reason}\"}}",
            CreatedBy = UserId
        });

        await _db.SaveChangesAsync(ct);
        return Ok(cn);
    }
}

public record CreateStructureRequest(string Name, long AcademicYearId, long TermId, long? GradeId, long? StreamId, long? StudentId, string Currency, List<StructureItemDto> Items);
public record StructureItemDto(long FeeItemId, string Description, decimal Amount, string Currency, int Quantity);
public record GenerateTermRequest(long AcademicYearId, long TermId);
public record RecordPaymentRequestDto(long StudentId, decimal Amount, string Currency, string Method, string? Reference, DateTime PaymentDate, string? ProofUrl, List<ManualAllocationDto>? ManualAllocations);
public record ReversePaymentRequest(string Reason);
public record CreateFeeItemRequest(string Name, string Code, string Recurrence, bool IsProratable, bool IsOptional, string? GlCode, string? Description);
public record CreateCreditNoteRequest(long InvoiceId, decimal Amount, string Currency, string Reason);
