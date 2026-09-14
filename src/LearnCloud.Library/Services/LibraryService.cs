using LearnCloud.Library.DTOs;
using LearnCloud.Library.Entities;
using LearnCloud.Fees.Entities;
using LearnCloud.MultiTenancy.Context;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Library.Services;

public interface ILibraryService
{
    Task<LoanDto> IssueAsync(long tenantId, long userId, IssueRequest req, CancellationToken ct = default);
    Task<LoanDto> ReturnAsync(long tenantId, long userId, ReturnRequest req, CancellationToken ct = default);
    Task<LoanDto> RenewAsync(long tenantId, long userId, RenewRequest req, CancellationToken ct = default);
    Task<FineDto> CalculateOverdueFineAsync(long tenantId, long loanId, CancellationToken ct = default);
    Task<FineDto> PostFineToFeeAccountAsync(long tenantId, long fineId, CancellationToken ct = default);
    Task<ReservationDto> ReserveAsync(long tenantId, CreateReservationRequest req, CancellationToken ct = default);
    Task<FastIssueResponse> FastIssueAsync(long tenantId, long userId, string barcodeOrAccession, long memberId, CancellationToken ct = default);
    Task<FastReturnResponse> FastReturnAsync(long tenantId, long userId, string barcodeOrAccession, string? condition, CancellationToken ct = default);
}

public class LibraryService : ILibraryService
{
    private readonly LearnCloudDbContext _db;
    private readonly Fees.Services.FeeCalculationService _feeCalc;

    public LibraryService(LearnCloudDbContext db, Fees.Services.FeeCalculationService feeCalc)
    {
        _db = db;
        _feeCalc = feeCalc;
    }

    // Fast issue and return screen designed for barcode scanner, and typing when scanner fails
    public async Task<FastIssueResponse> FastIssueAsync(long tenantId, long userId, string barcodeOrAccession, long memberId, CancellationToken ct = default)
    {
        // Barcode scanner types quickly and sends Enter - we trim spaces, handle mixed case, handle trailing spaces
        var cleaned = barcodeOrAccession.Trim().Replace(" ", "").ToUpperInvariant();

        // Try find by barcode or accession number (case-insensitive, trimmed)
        var copy = await _db.Set<BookCopy>()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && (c.Barcode.ToUpper() == cleaned || c.AccessionNumber.ToUpper() == cleaned || c.Barcode == barcodeOrAccession.Trim() || c.AccessionNumber == barcodeOrAccession.Trim()), ct);

        if (copy == null)
        {
            // Try with original input without upper (for typing when scanner fails, allow partial)
            copy = await _db.Set<BookCopy>()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && (c.Barcode.Contains(barcodeOrAccession.Trim()) || c.AccessionNumber.Contains(barcodeOrAccession.Trim())), ct);

            if (copy == null)
                return new FastIssueResponse(false, null, $"Copy not found for barcode/accession '{barcodeOrAccession}' - try typing accession number", barcodeOrAccession, memberId);
        }

        // Check membership borrowing limits
        var member = await _db.Set<LibraryMember>().FirstOrDefaultAsync(m => m.Id == memberId && m.TenantId == tenantId && !m.IsDeleted, ct);
        if (member == null) return new FastIssueResponse(false, null, $"Member {memberId} not found", barcodeOrAccession, memberId);

        var config = await _db.Set<MembershipConfig>().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.MembershipType == member.MemberType && !c.IsDeleted, ct);
        if (config == null) config = new MembershipConfig { MaxBooks = 3, LoanPeriodDays = 14, MaxRenewals = 1, FinePerDay = 1.00m, Currency = "USD" };

        if (member.CurrentlyBorrowed >= config.MaxBooks)
            return new FastIssueResponse(false, null, $"Member {member.FullName} reached borrowing limit {config.MaxBooks} (currently {member.CurrentlyBorrowed})", barcodeOrAccession, memberId);

        if (copy.Status != "available")
            return new FastIssueResponse(false, null, $"Copy {copy.AccessionNumber} not available - status {copy.Status}", barcodeOrAccession, memberId);

        // Check reservations - if reserved by other member and waiting list, block
        var reservation = await _db.Set<Reservation>()
            .Where(r => r.TenantId == tenantId && r.BookId == copy.BookId && r.Status == "pending" && !r.IsDeleted)
            .OrderBy(r => r.QueuePosition).FirstOrDefaultAsync(ct);

        if (reservation != null && reservation.MemberId != memberId)
        {
            return new FastIssueResponse(false, null, $"Book {copy.BookId} reserved by {reservation.MemberId}, queue position {reservation.QueuePosition}. Waiting list, cannot issue to {member.FullName}", barcodeOrAccession, memberId);
        }

        // Issue - create loan
        var now = DateTime.UtcNow;
        var dueDate = now.AddDays(config.LoanPeriodDays);

        var loan = new Loan
        {
            TenantId = tenantId,
            BookCopyId = copy.Id,
            BookId = copy.BookId,
            MemberId = memberId,
            StudentId = member.StudentId,
            StaffId = member.StaffId,
            IssueDate = now,
            DueDate = dueDate,
            Status = "issued",
            IssuedByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<Loan>().Add(loan);

        copy.Status = "issued";
        copy.LastIssuedAt = now;
        copy.CurrentLoanId = loan.Id;

        member.CurrentlyBorrowed++;
        member.TotalBorrowed++;

        // If reservation fulfilled
        if (reservation != null && reservation.MemberId == memberId)
        {
            reservation.Status = "fulfilled";
            reservation.FulfilledAt = now;
            reservation.FulfilledLoanId = loan.Id;
        }

        await _db.SaveChangesAsync(ct);

        var book = await _db.Set<Book>().FirstOrDefaultAsync(b => b.Id == copy.BookId, ct);
        var loanDto = new LoanDto(loan.Id, copy.Id, book?.Title ?? $"Book {copy.BookId}", copy.AccessionNumber, copy.Barcode, memberId, member.FullName, loan.IssueDate, loan.DueDate, null, loan.Status, 0, false, 0);

        return new FastIssueResponse(true, loanDto, $"Issued {book?.Title} to {member.FullName}, due {dueDate:dd/MM/yyyy}", barcodeOrAccession, memberId);
    }

    public async Task<FastReturnResponse> FastReturnAsync(long tenantId, long userId, string barcodeOrAccession, string? condition, CancellationToken ct = default)
    {
        var cleaned = barcodeOrAccession.Trim().Replace(" ", "").ToUpperInvariant();

        var copy = await _db.Set<BookCopy>()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && (c.Barcode.ToUpper() == cleaned || c.AccessionNumber.ToUpper() == cleaned || c.Barcode == barcodeOrAccession.Trim() || c.AccessionNumber == barcodeOrAccession.Trim()), ct);

        if (copy == null)
        {
            copy = await _db.Set<BookCopy>()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && (c.Barcode.Contains(barcodeOrAccession.Trim()) || c.AccessionNumber.Contains(barcodeOrAccession.Trim())), ct);
            if (copy == null)
                return new FastReturnResponse(false, null, null, $"Copy not found for '{barcodeOrAccession}' - try typing accession number");
        }

        var loan = await _db.Set<Loan>()
            .Where(l => l.TenantId == tenantId && l.BookCopyId == copy.Id && l.Status == "issued" && !l.IsDeleted)
            .OrderByDescending(l => l.IssueDate).FirstOrDefaultAsync(ct);

        if (loan == null)
            return new FastReturnResponse(false, null, null, $"No active loan for copy {copy.AccessionNumber} - already returned?");

        var now = DateTime.UtcNow;
        loan.ReturnDate = now;
        loan.Status = "returned";
        loan.ReturnedByUserId = userId;

        copy.Status = condition == "damaged" ? "damaged" : "available";
        copy.Condition = condition ?? "good";
        copy.LastReturnedAt = now;
        copy.CurrentLoanId = null;

        var member = await _db.Set<LibraryMember>().FirstOrDefaultAsync(m => m.Id == loan.MemberId, ct);
        if (member != null && member.CurrentlyBorrowed > 0) member.CurrentlyBorrowed--;

        // Overdue tracking
        FineDto? fineGenerated = null;
        if (now.Date > loan.DueDate.Date)
        {
            var daysOverdue = (now.Date - loan.DueDate.Date).Days;
            var config = await _db.Set<MembershipConfig>().FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted, ct);
            var finePerDay = config?.FinePerDay ?? 1.00m;
            var amount = Math.Round(daysOverdue * finePerDay, 2, MidpointRounding.AwayFromZero);
            var maxFine = config?.MaxFine ?? 50.00m;
            if (amount > maxFine) amount = maxFine;

            var fine = new Fine
            {
                TenantId = tenantId,
                LoanId = loan.Id,
                MemberId = loan.MemberId,
                StudentId = loan.StudentId,
                FineType = "overdue",
                Amount = amount,
                Currency = config?.Currency ?? "USD",
                DaysOverdue = daysOverdue,
                FinePerDay = finePerDay,
                Status = "pending",
                CreatedBy = userId
            };
            _db.Set<Fine>().Add(fine);
            await _db.SaveChangesAsync(ct);

            fineGenerated = new FineDto(fine.Id, loan.Id, (await _db.Set<Book>().FirstOrDefaultAsync(b => b.Id == copy.BookId, ct))?.Title ?? "", loan.MemberId, member?.FullName ?? "", fine.FineType, fine.Amount, fine.Currency, fine.DaysOverdue, fine.Status, fine.PostedToFeeAccount, fine.FeeInvoiceId);
        }

        await _db.SaveChangesAsync(ct);

        var book = await _db.Set<Book>().FirstOrDefaultAsync(b => b.Id == copy.BookId, ct);
        var loanDto = new LoanDto(loan.Id, copy.Id, book?.Title ?? "", copy.AccessionNumber, copy.Barcode, loan.MemberId, member?.FullName ?? "", loan.IssueDate, loan.DueDate, loan.ReturnDate, loan.Status, loan.RenewalCount, false, loan.DueDate < now ? (now.Date - loan.DueDate.Date).Days : 0);

        return new FastReturnResponse(true, loanDto, fineGenerated, fineGenerated != null ? $"Returned with overdue fine {fineGenerated.Amount} {fineGenerated.Currency} for {fineGenerated.DaysOverdue} days" : $"Returned {book?.Title} successfully");
    }

    public async Task<LoanDto> IssueAsync(long tenantId, long userId, IssueRequest req, CancellationToken ct = default)
    {
        var result = await FastIssueAsync(tenantId, userId, req.BarcodeOrAccession, req.MemberId, ct);
        if (!result.Success) throw new InvalidOperationException(result.Message);
        return result.Loan!;
    }

    public async Task<LoanDto> ReturnAsync(long tenantId, long userId, ReturnRequest req, CancellationToken ct = default)
    {
        var result = await FastReturnAsync(tenantId, userId, req.BarcodeOrAccession, req.Condition, ct);
        if (!result.Success) throw new InvalidOperationException(result.Message);
        return result.Loan!;
    }

    public async Task<LoanDto> RenewAsync(long tenantId, long userId, RenewRequest req, CancellationToken ct = default)
    {
        var loan = await _db.Set<Loan>().FirstOrDefaultAsync(l => l.Id == req.LoanId && l.TenantId == tenantId && !l.IsDeleted, ct) ?? throw new InvalidOperationException("Loan not found");
        var member = await _db.Set<LibraryMember>().FirstOrDefaultAsync(m => m.Id == loan.MemberId, ct) ?? throw new InvalidOperationException("Member not found");
        var config = await _db.Set<MembershipConfig>().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.MembershipType == member.MemberType && !c.IsDeleted, ct);

        if (loan.RenewalCount >= (config?.MaxRenewals ?? 1))
            throw new InvalidOperationException($"Max renewals {config?.MaxRenewals} reached");

        var previousDue = loan.DueDate;
        loan.DueDate = loan.DueDate.AddDays(config?.LoanPeriodDays ?? 14);
        loan.RenewalCount++;

        var renewal = new LoanRenewal
        {
            TenantId = tenantId,
            LoanId = loan.Id,
            RenewalDate = DateTime.UtcNow,
            PreviousDueDate = previousDue,
            NewDueDate = loan.DueDate,
            RenewalNumber = loan.RenewalCount,
            ApprovedByUserId = userId,
            Reason = req.Reason,
            CreatedBy = userId
        };
        _db.Set<LoanRenewal>().Add(renewal);
        await _db.SaveChangesAsync(ct);

        var book = await _db.Set<Book>().FirstOrDefaultAsync(b => b.Id == loan.BookId, ct);
        var copy = await _db.Set<BookCopy>().FirstOrDefaultAsync(c => c.Id == loan.BookCopyId, ct);
        return new LoanDto(loan.Id, loan.BookCopyId, book?.Title ?? "", copy?.AccessionNumber ?? "", copy?.Barcode ?? "", loan.MemberId, member.FullName, loan.IssueDate, loan.DueDate, loan.ReturnDate, loan.Status, loan.RenewalCount, false, 0);
    }

    public async Task<FineDto> CalculateOverdueFineAsync(long tenantId, long loanId, CancellationToken ct = default)
    {
        var loan = await _db.Set<Loan>().FirstOrDefaultAsync(l => l.Id == loanId && l.TenantId == tenantId && !l.IsDeleted, ct) ?? throw new InvalidOperationException("Loan not found");
        var member = await _db.Set<LibraryMember>().FirstOrDefaultAsync(m => m.Id == loan.MemberId, ct);
        var memberType = member?.MemberType ?? "student"; // EF expression trees cannot contain ?.
        var config = await _db.Set<MembershipConfig>().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.MembershipType == memberType && !c.IsDeleted, ct);

        var now = DateTime.UtcNow.Date;
        var daysOverdue = (now - loan.DueDate.Date).Days;
        if (daysOverdue <= 0) throw new InvalidOperationException("Not overdue");

        var finePerDay = config?.FinePerDay ?? 1.00m;
        var amount = Math.Round(daysOverdue * finePerDay, 2, MidpointRounding.AwayFromZero);
        var maxFine = config?.MaxFine ?? 50.00m;
        if (amount > maxFine) amount = maxFine;

        var existingFine = await _db.Set<Fine>().FirstOrDefaultAsync(f => f.LoanId == loanId && f.TenantId == tenantId && f.FineType == "overdue" && !f.IsDeleted, ct);
        if (existingFine != null)
        {
            existingFine.Amount = amount;
            existingFine.DaysOverdue = daysOverdue;
            await _db.SaveChangesAsync(ct);
            var book = await _db.Set<Book>().FirstOrDefaultAsync(b => b.Id == loan.BookId, ct);
            return new FineDto(existingFine.Id, loanId, book?.Title ?? "", loan.MemberId, member?.FullName ?? "", existingFine.FineType, existingFine.Amount, existingFine.Currency, existingFine.DaysOverdue, existingFine.Status, existingFine.PostedToFeeAccount, existingFine.FeeInvoiceId);
        }

        var fine = new Fine
        {
            TenantId = tenantId,
            LoanId = loanId,
            MemberId = loan.MemberId,
            StudentId = loan.StudentId,
            FineType = "overdue",
            Amount = amount,
            Currency = config?.Currency ?? "USD",
            DaysOverdue = daysOverdue,
            FinePerDay = finePerDay,
            Status = "pending"
        };
        _db.Set<Fine>().Add(fine);
        await _db.SaveChangesAsync(ct);

        var book2 = await _db.Set<Book>().FirstOrDefaultAsync(b => b.Id == loan.BookId, ct);
        return new FineDto(fine.Id, loanId, book2?.Title ?? "", loan.MemberId, member?.FullName ?? "", fine.FineType, fine.Amount, fine.Currency, fine.DaysOverdue, fine.Status, fine.PostedToFeeAccount, fine.FeeInvoiceId);
    }

    public async Task<FineDto> PostFineToFeeAccountAsync(long tenantId, long fineId, CancellationToken ct = default)
    {
        var fine = await _db.Set<Fine>().FirstOrDefaultAsync(f => f.Id == fineId && f.TenantId == tenantId && !f.IsDeleted, ct) ?? throw new InvalidOperationException("Fine not found");

        if (fine.PostedToFeeAccount) throw new InvalidOperationException("Fine already posted to fee account");
        if (fine.StudentId == null) throw new InvalidOperationException("Fine has no student - cannot post to learner's fee account, only for learners");

        // Post to learner's fee account through existing fee services (without altering them)
        // We create a fee invoice via existing fee service: create invoice with fee item "Library Fine"
        // For V1, we simulate by creating a FeeInvoice directly using existing entity structure - consuming existing fee entities without altering them
        // In real app, we would call _feeService.CreateFineInvoiceAsync which internally uses FeeCalculationService

        var studentId = fine.StudentId.Value;

        // Find or create fee item for library fine
        var feeItem = await _db.Set<FeeItem>().FirstOrDefaultAsync(fi => fi.TenantId == tenantId && fi.Code == "LIB_FINE" && !fi.IsDeleted, ct);
        if (feeItem == null)
        {
            feeItem = new FeeItem { TenantId = tenantId, Name = "Library Fine", Code = "LIB_FINE", Recurrence = FeeItemRecurrence.OneOff, IsProratable = false, IsOptional = false, Description = "Library overdue/lost/damaged fine" };
            _db.Set<FeeItem>().Add(feeItem);
            await _db.SaveChangesAsync(ct);
        }

        // Create fee invoice for learner
        var feeInvoice = new FeeInvoice
        {
            TenantId = tenantId,
            InvoiceNumber = $"LIB-FINE-{DateTime.UtcNow:yyyyMMdd}-{fine.Id:D5}",
            AcademicYearId = 2026, // would get current academic year
            TermId = 1,
            StudentId = studentId,
            SubtotalAmount = fine.Amount,
            DiscountAmount = 0m,
            TotalAmount = fine.Amount,
            AmountPaid = 0m,
            BalanceDue = fine.Amount,
            Currency = fine.Currency,
            IssueDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(7),
            Status = InvoiceStatus.Issued,
            StructureHash = $"library_fine_{fine.Id}",
            CreatedBy = fine.CreatedBy
        };
        _db.Set<FeeInvoice>().Add(feeInvoice);
        await _db.SaveChangesAsync(ct);

        var invoiceItem = new FeeInvoiceItem
        {
            TenantId = tenantId,
            InvoiceId = feeInvoice.Id,
            FeeItemId = feeItem.Id,
            Description = $"Library fine - {fine.FineType} - Loan {fine.LoanId} - {fine.DaysOverdue} days overdue",
            Quantity = 1,
            UnitAmount = fine.Amount,
            LineTotal = fine.Amount,
            Currency = fine.Currency
        };
        _db.Set<FeeInvoiceItem>().Add(invoiceItem);
        await _db.SaveChangesAsync(ct);

        fine.PostedToFeeAccount = true;
        fine.FeeInvoiceId = feeInvoice.Id;
        fine.Status = "posted_to_fee_account";
        await _db.SaveChangesAsync(ct);

        var loan = await _db.Set<Loan>().FirstOrDefaultAsync(l => l.Id == fine.LoanId, ct)
            ?? throw new InvalidOperationException($"Loan {fine.LoanId} for fine {fine.Id} not found");
        var book = await _db.Set<Book>().FirstOrDefaultAsync(b => b.Id == loan.BookId, ct);
        var member = await _db.Set<LibraryMember>().FirstOrDefaultAsync(m => m.Id == fine.MemberId, ct);

        return new FineDto(fine.Id, fine.LoanId, book?.Title ?? "", fine.MemberId, member?.FullName ?? "", fine.FineType, fine.Amount, fine.Currency, fine.DaysOverdue, fine.Status, fine.PostedToFeeAccount, fine.FeeInvoiceId);
    }

    public async Task<ReservationDto> ReserveAsync(long tenantId, CreateReservationRequest req, CancellationToken ct = default)
    {
        var book = await _db.Set<Book>().FirstOrDefaultAsync(b => b.Id == req.BookId && b.TenantId == tenantId && !b.IsDeleted, ct) ?? throw new InvalidOperationException("Book not found");
        var member = await _db.Set<LibraryMember>().FirstOrDefaultAsync(m => m.Id == req.MemberId && m.TenantId == tenantId && !m.IsDeleted, ct) ?? throw new InvalidOperationException("Member not found");

        var config = await _db.Set<MembershipConfig>().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.MembershipType == member.MemberType && !c.IsDeleted, ct);
        if (config != null && !config.AllowReservations) throw new InvalidOperationException("Reservations not allowed for this membership type");

        // Check how many pending reservations for this book to determine queue position
        var pendingCount = await _db.Set<Reservation>().CountAsync(r => r.TenantId == tenantId && r.BookId == req.BookId && r.Status == "pending" && !r.IsDeleted, ct);

        var reservation = new Reservation
        {
            TenantId = tenantId,
            BookId = req.BookId,
            MemberId = req.MemberId,
            StudentId = member.StudentId,
            ReservationDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            Status = "pending",
            QueuePosition = pendingCount + 1,
            CreatedBy = member.Id
        };
        _db.Set<Reservation>().Add(reservation);
        await _db.SaveChangesAsync(ct);

        return new ReservationDto(reservation.Id, reservation.BookId, book.Title, reservation.MemberId, member.FullName, reservation.ReservationDate, reservation.ExpiryDate, reservation.Status, reservation.QueuePosition);
    }
}
