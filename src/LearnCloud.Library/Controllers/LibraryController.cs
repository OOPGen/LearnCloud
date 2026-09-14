using LearnCloud.Library.Entities;
using LearnCloud.Library.DTOs;
using LearnCloud.Library.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Library.Controllers;

[ApiController]
[Route("api/library")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class LibraryController : ControllerBase
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILibraryService _libraryService;

    public LibraryController(LearnCloudDbContext db, ITenantContext tenantContext, ILibraryService libraryService)
    {
        _db = db; _tenantContext = tenantContext; _libraryService = libraryService;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Catalogue
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("books")]
    public async Task<IActionResult> GetBooks([FromQuery] string? search, [FromQuery] long? categoryId, CancellationToken ct)
    {
        var q = _db.Set<Entities.Book>().Where(b => b.TenantId == TenantId && !b.IsDeleted);
        if (!string.IsNullOrEmpty(search)) q = q.Where(b => b.Title.Contains(search) || b.Author.Contains(search) || (b.ISBN != null && b.ISBN.Contains(search)));
        if (categoryId.HasValue) q = q.Where(b => b.CategoryId == categoryId.Value);
        var books = await q.Include(b => b.Category).Take(100).ToListAsync(ct);
        return Ok(books.Select(b => new BookDto(b.Id, b.Title, b.Author, b.ISBN, b.CategoryId, b.Category.Name, b.ShelfLocation, b.TotalCopies, b.AvailableCopies, b.ReplacementPrice, b.Currency, b.Status, b.CoverImageUrl)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("books")]
    [Authorize(Roles = "LIBRARIAN,SCHOOL_ADMIN,HEAD_TEACHER")]
    public async Task<IActionResult> CreateBook([FromBody] CreateBookRequest req, CancellationToken ct)
    {
        var book = new Entities.Book
        {
            TenantId = TenantId,
            Title = req.Title,
            Author = req.Author,
            ISBN = req.ISBN,
            CategoryId = req.CategoryId,
            ShelfLocation = req.ShelfLocation,
            TotalCopies = req.TotalCopies,
            AvailableCopies = req.TotalCopies,
            ReplacementPrice = req.ReplacementPrice,
            Currency = req.Currency,
            Publisher = req.Publisher,
            PublicationYear = req.PublicationYear,
            Description = req.Description,
            CreatedBy = UserId
        };
        _db.Set<Entities.Book>().Add(book);
        await _db.SaveChangesAsync(ct);

        // Auto-create copies with barcode/accession
        for (int i = 1; i <= req.TotalCopies; i++)
        {
            var copy = new Entities.BookCopy
            {
                TenantId = TenantId,
                BookId = book.Id,
                AccessionNumber = $"ACC-{DateTime.UtcNow.Year}-{(await _db.Set<Entities.BookCopy>().CountAsync(c => c.TenantId == TenantId, ct) + i):D5}",
                Barcode = $"BC-{book.Id:D5}-{i:D3}-{new Random().Next(100,999)}",
                ShelfLocation = req.ShelfLocation,
                Status = "available",
                Condition = "good",
                CreatedBy = UserId
            };
            _db.Set<Entities.BookCopy>().Add(copy);
        }
        await _db.SaveChangesAsync(ct);

        return Ok(book);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("books/{id:long}/copies")]
    public async Task<IActionResult> GetCopies(long id, CancellationToken ct)
    {
        var copies = await _db.Set<Entities.BookCopy>().Where(c => c.TenantId == TenantId && c.BookId == id && !c.IsDeleted).ToListAsync(ct);
        return Ok(copies.Select(c => new BookCopyDto(c.Id, c.BookId, "", c.AccessionNumber, c.Barcode, c.ShelfLocation, c.Status, c.Condition, c.LastIssuedAt)));
    }

    // Fast issue and return screen designed for barcode scanner, and typing when scanner fails
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("issue/fast")]
    [Authorize(Roles = "LIBRARIAN,SCHOOL_ADMIN,TEACHER")]
    public async Task<IActionResult> FastIssue([FromBody] IssueRequest req, CancellationToken ct)
    {
        var result = await _libraryService.FastIssueAsync(TenantId, UserId, req.BarcodeOrAccession, req.MemberId, ct);
        if (!result.Success) return BadRequest(new { message = result.Message });
        return Ok(result);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("return/fast")]
    [Authorize(Roles = "LIBRARIAN,SCHOOL_ADMIN,TEACHER")]
    public async Task<IActionResult> FastReturn([FromBody] ReturnRequest req, CancellationToken ct)
    {
        var result = await _libraryService.FastReturnAsync(TenantId, UserId, req.BarcodeOrAccession, req.Condition, ct);
        if (!result.Success) return BadRequest(new { message = result.Message });
        return Ok(result);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("issue")]
    public async Task<IActionResult> Issue([FromBody] IssueRequest req, CancellationToken ct)
    {
        var loan = await _libraryService.IssueAsync(TenantId, UserId, req, ct);
        return Ok(loan);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("return")]
    public async Task<IActionResult> Return([FromBody] ReturnRequest req, CancellationToken ct)
    {
        var loan = await _libraryService.ReturnAsync(TenantId, UserId, req, ct);
        return Ok(loan);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("renew")]
    public async Task<IActionResult> Renew([FromBody] RenewRequest req, CancellationToken ct)
    {
        var loan = await _libraryService.RenewAsync(TenantId, UserId, req, ct);
        return Ok(loan);
    }

    // Reservations and waiting list
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("reservations")]
    public async Task<IActionResult> Reserve([FromBody] CreateReservationRequest req, CancellationToken ct)
    {
        var res = await _libraryService.ReserveAsync(TenantId, req, ct);
        return Ok(res);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reservations")]
    public async Task<IActionResult> GetReservations([FromQuery] long? bookId, CancellationToken ct)
    {
        var q = _db.Set<Entities.Reservation>().Where(r => r.TenantId == TenantId && !r.IsDeleted);
        if (bookId.HasValue) q = q.Where(r => r.BookId == bookId.Value);
        var list = await q.OrderBy(r => r.QueuePosition).Take(100).ToListAsync(ct);
        return Ok(list);
    }

    // Overdue tracking with fines that post to learner's fee account through existing fee services
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("fines/overdue")]
    public async Task<IActionResult> GetOverdueFines(CancellationToken ct)
    {
        var fines = await _db.Set<Entities.Fine>().Where(f => f.TenantId == TenantId && f.FineType == "overdue" && !f.IsDeleted && f.Status == "pending").Include(f => f.Loan).ToListAsync(ct);
        return Ok(fines.Select(f => new FineDto(f.Id, f.LoanId, "", f.MemberId, "", f.FineType, f.Amount, f.Currency, f.DaysOverdue, f.Status, f.PostedToFeeAccount, f.FeeInvoiceId)));
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("fines/{id:long}/post-to-fee-account")]
    [Authorize(Roles = "BURSAR,SCHOOL_ADMIN,LIBRARIAN")]
    public async Task<IActionResult> PostFineToFeeAccount(long id, CancellationToken ct)
    {
        var fineDto = await _libraryService.PostFineToFeeAccountAsync(TenantId, id, ct);
        return Ok(fineDto);
    }

    // Lost and damaged handling with replacement charge
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("lost-damaged")]
    [Authorize(Roles = "LIBRARIAN,SCHOOL_ADMIN")]
    public async Task<IActionResult> LostDamaged([FromBody] LostDamagedRequest req, CancellationToken ct)
    {
        var copy = await _db.Set<Entities.BookCopy>().FirstOrDefaultAsync(c => c.Id == req.BookCopyId && c.TenantId == TenantId && !c.IsDeleted, ct);
        if (copy == null) return NotFound();
        var loan = await _db.Set<Entities.Loan>().FirstOrDefaultAsync(l => l.Id == req.LoanId && l.TenantId == TenantId && !l.IsDeleted, ct);
        if (loan == null) return NotFound();

        var record = new Entities.LostDamagedRecord
        {
            TenantId = TenantId,
            BookCopyId = req.BookCopyId,
            LoanId = req.LoanId,
            MemberId = loan.MemberId,
            Type = req.Type,
            Condition = req.Condition,
            ReplacementCharge = req.ReplacementCharge,
            FineAmount = req.FineAmount,
            Currency = req.Currency,
            Status = "pending",
            CreatedBy = UserId
        };
        _db.Set<Entities.LostDamagedRecord>().Add(record);

        copy.Status = req.Type == "lost" ? "lost" : "damaged";
        copy.Condition = req.Condition;

        // Create fine for replacement charge that posts to fee account
        var fine = new Entities.Fine
        {
            TenantId = TenantId,
            LoanId = req.LoanId,
            MemberId = loan.MemberId,
            StudentId = loan.StudentId,
            FineType = req.Type,
            Amount = req.ReplacementCharge + req.FineAmount,
            Currency = req.Currency,
            DaysOverdue = 0,
            FinePerDay = 0,
            Status = "pending",
            CreatedBy = UserId
        };
        _db.Set<Entities.Fine>().Add(fine);
        await _db.SaveChangesAsync(ct);

        // Post to fee account through existing fee services
        var fineDto = await _libraryService.PostFineToFeeAccountAsync(TenantId, fine.Id, ct);

        return Ok(new { record, fine = fineDto });
    }

    // Stock take with discrepancy report
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("stock-takes")]
    [Authorize(Roles = "LIBRARIAN,SCHOOL_ADMIN")]
    public async Task<IActionResult> CreateStockTake([FromBody] CreateStockTakeRequest req, CancellationToken ct)
    {
        var st = new Entities.StockTake { TenantId = TenantId, Name = req.Name, StartedAt = DateTime.UtcNow, Status = "in_progress", CreatedByUserId = UserId, TotalExpected = await _db.Set<Entities.BookCopy>().CountAsync(c => c.TenantId == TenantId && !c.IsDeleted, ct), CreatedBy = UserId };
        _db.Set<Entities.StockTake>().Add(st);
        await _db.SaveChangesAsync(ct);
        return Ok(st);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("stock-takes/{id:long}/count")]
    public async Task<IActionResult> CountStockTakeItem(long id, [FromBody] CountStockTakeItemRequest req, CancellationToken ct)
    {
        var stockTake = await _db.Set<Entities.StockTake>().FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId && !s.IsDeleted, ct);
        if (stockTake == null) return NotFound();

        var copy = await _db.Set<Entities.BookCopy>().FirstOrDefaultAsync(c => c.Id == req.BookCopyId && c.TenantId == TenantId && !c.IsDeleted, ct);
        if (copy == null) return NotFound();

        var item = new Entities.StockTakeItem
        {
            TenantId = TenantId,
            StockTakeId = id,
            BookCopyId = req.BookCopyId,
            ExpectedStatus = copy.Status,
            CountedStatus = req.CountedStatus,
            DiscrepancyType = copy.Status != req.CountedStatus ? (req.CountedStatus == "missing" ? "missing" : copy.Status == "available" && req.CountedStatus != "available" ? "wrong_location" : "damaged") : "none",
            Notes = req.Notes,
            CountedByUserId = UserId,
            CreatedBy = UserId
        };
        _db.Set<Entities.StockTakeItem>().Add(item);

        stockTake.TotalCounted++;
        if (item.DiscrepancyType != "none") stockTake.Discrepancies++;

        await _db.SaveChangesAsync(ct);
        return Ok(item);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("stock-takes/{id:long}/discrepancy-report")]
    public async Task<IActionResult> GetDiscrepancyReport(long id, CancellationToken ct)
    {
        var stockTake = await _db.Set<Entities.StockTake>().FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId && !s.IsDeleted, ct);
        if (stockTake == null) return NotFound();
        var items = await _db.Set<Entities.StockTakeItem>().Where(i => i.StockTakeId == id && i.TenantId == TenantId && i.DiscrepancyType != "none" && !i.IsDeleted).Include(i => i.BookCopy).ToListAsync(ct);
        return Ok(new { stockTake, discrepancies = items, totalExpected = stockTake.TotalExpected, totalCounted = stockTake.TotalCounted, discrepanciesCount = stockTake.Discrepancies });
    }

    // Reports on circulation, popular titles, overdue items and inventory value
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/circulation")]
    public async Task<IActionResult> GetCirculationReport([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var loans = await _db.Set<Entities.Loan>().Where(l => l.TenantId == TenantId && l.IssueDate >= from && l.IssueDate <= to && !l.IsDeleted).ToListAsync(ct);
        var returns = await _db.Set<Entities.Loan>().Where(l => l.TenantId == TenantId && l.ReturnDate.HasValue && l.ReturnDate >= from && l.ReturnDate <= to && !l.IsDeleted).ToListAsync(ct);
        var renewals = await _db.Set<Entities.LoanRenewal>().Where(r => r.TenantId == TenantId && r.RenewalDate >= from && r.RenewalDate <= to && !r.IsDeleted).ToListAsync(ct);

        var daily = new List<object>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
        {
            daily.Add(new { date = d, issues = loans.Count(l => l.IssueDate.Date == d), returns = returns.Count(r => r.ReturnDate!.Value.Date == d) });
        }

        return Ok(new { totalIssues = loans.Count, totalReturns = returns.Count, totalRenewals = renewals.Count, daily });
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/popular-titles")]
    public async Task<IActionResult> GetPopularTitles([FromQuery] int top = 10, CancellationToken ct = default)
    {
        var popular = await _db.Set<Entities.Loan>()
            .Where(l => l.TenantId == TenantId && !l.IsDeleted)
            .GroupBy(l => l.BookId)
            .Select(g => new { bookId = g.Key, timesBorrowed = g.Count() })
            .OrderByDescending(x => x.timesBorrowed)
            .Take(top)
            .ToListAsync(ct);

        var result = new List<PopularTitleDto>();
        foreach (var p in popular)
        {
            var book = await _db.Set<Entities.Book>().FirstOrDefaultAsync(b => b.Id == p.bookId, ct);
            if (book == null) continue;
            result.Add(new PopularTitleDto(book.Id, book.Title, book.Author, p.timesBorrowed, book.AvailableCopies, book.TotalCopies));
        }
        return Ok(result);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/overdue")]
    public async Task<IActionResult> GetOverdueReport(CancellationToken ct)
    {
        var overdue = await _db.Set<Entities.Loan>().Where(l => l.TenantId == TenantId && l.Status == "issued" && l.DueDate < DateTime.UtcNow && !l.IsDeleted).Include(l => l.Member).ToListAsync(ct);
        var totalFines = await _db.Set<Entities.Fine>().Where(f => f.TenantId == TenantId && f.Status == "pending" && !f.IsDeleted).SumAsync(f => f.Amount, ct);
        return Ok(new OverdueReportDto(overdue.Select(l => new LoanDto(l.Id, l.BookCopyId, "", "", "", l.MemberId, l.Member.FullName, l.IssueDate, l.DueDate, l.ReturnDate, l.Status, l.RenewalCount, true, (DateTime.UtcNow.Date - l.DueDate.Date).Days)).ToList(), totalFines, "USD"));
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/inventory-value")]
    public async Task<IActionResult> GetInventoryValue(CancellationToken ct)
    {
        var books = await _db.Set<Entities.Book>().Where(b => b.TenantId == TenantId && !b.IsDeleted).ToListAsync(ct);
        var copies = await _db.Set<Entities.BookCopy>().Where(c => c.TenantId == TenantId && !c.IsDeleted).CountAsync(ct);
        var available = await _db.Set<Entities.BookCopy>().Where(c => c.TenantId == TenantId && c.Status == "available" && !c.IsDeleted).CountAsync(ct);
        var totalValue = books.Sum(b => b.TotalCopies * b.ReplacementPrice);
        var byCategory = await _db.Set<Entities.Book>().Where(b => b.TenantId == TenantId && !b.IsDeleted).GroupBy(b => b.CategoryId).Select(g => new { categoryId = g.Key, copies = g.Sum(b => b.TotalCopies), value = g.Sum(b => b.TotalCopies * b.ReplacementPrice) }).ToListAsync(ct);

        var categoryValues = new List<CategoryValueDto>();
        foreach (var c in byCategory)
        {
            var cat = await _db.Set<LibraryCategory>().FirstOrDefaultAsync(cc => cc.Id == c.categoryId, ct);
            categoryValues.Add(new CategoryValueDto(c.categoryId, cat?.Name ?? c.categoryId.ToString(), c.copies, c.value, "USD"));
        }

        return Ok(new InventoryValueDto(books.Count, copies, available, totalValue, "USD", categoryValues));
    }
}