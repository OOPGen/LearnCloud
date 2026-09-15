using LearnCloud.Core.DTOs;
using LearnCloud.MultiTenancy.Context;

namespace LearnCloud.Core.Services;

public interface IAcademicCalendarService
{
    Task<List<CalendarYearDto>> ListYearsAsync(long tenantId, CancellationToken ct = default);
    Task<CalendarYearDto> GetYearAsync(long tenantId, long yearId, CancellationToken ct = default);
    Task<CurrentCalendarDto> GetCurrentAsync(long tenantId, CancellationToken ct = default);
    Task<CalendarYearDto> CreateYearAsync(long tenantId, long userId, CreateAcademicYearRequest req, CancellationToken ct = default);
    Task<CalendarYearDto> UpdateYearAsync(long tenantId, long userId, long yearId, UpdateAcademicYearRequest req, CancellationToken ct = default);
    Task<CalendarYearDto> SetCurrentYearAsync(long tenantId, long userId, long yearId, CancellationToken ct = default);
    Task DeleteYearAsync(long tenantId, long userId, long yearId, CancellationToken ct = default);

    Task<CalendarTermDto> CreateTermAsync(long tenantId, long userId, long yearId, CreateTermRequest req, CancellationToken ct = default);
    Task<CalendarTermDto> UpdateTermAsync(long tenantId, long userId, long termId, UpdateTermRequest req, CancellationToken ct = default);
    Task<CalendarTermDto> SetCurrentTermAsync(long tenantId, long userId, long termId, CancellationToken ct = default);
    Task DeleteTermAsync(long tenantId, long userId, long termId, CancellationToken ct = default);

    /// <summary>
    /// Makes the year's terms exactly <paramref name="terms"/>, matched by term number: used by
    /// the setup wizard, which submits the whole list at once. Validates the set as a whole, so
    /// shifting every term's dates does not trip over the old dates of the next term.
    /// </summary>
    Task<CalendarYearDto> ReplaceTermsAsync(long tenantId, long userId, long yearId, IReadOnlyList<CreateTermRequest> terms, CancellationToken ct = default);
}

public class AcademicCalendarService : IAcademicCalendarService
{
    private readonly LearnCloudDbContext _db;

    public AcademicCalendarService(LearnCloudDbContext db) => _db = db;

    public async Task<List<CalendarYearDto>> ListYearsAsync(long tenantId, CancellationToken ct = default)
    {
        var years = await _db.Set<AcademicYear>().Where(y => y.TenantId == tenantId).OrderByDescending(y => y.StartDate).ToListAsync(ct);
        var terms = await _db.Set<Term>().Where(t => t.TenantId == tenantId).OrderBy(t => t.TermNumber).ToListAsync(ct);
        return years.Select(y => ToDto(y, terms.Where(t => t.AcademicYearId == y.Id))).ToList();
    }

    public async Task<CalendarYearDto> GetYearAsync(long tenantId, long yearId, CancellationToken ct = default)
    {
        var year = await FindYearAsync(tenantId, yearId, ct);
        var terms = await _db.Set<Term>().Where(t => t.TenantId == tenantId && t.AcademicYearId == yearId).OrderBy(t => t.TermNumber).ToListAsync(ct);
        return ToDto(year, terms);
    }

    public async Task<CurrentCalendarDto> GetCurrentAsync(long tenantId, CancellationToken ct = default)
    {
        var year = await _db.Set<AcademicYear>().FirstOrDefaultAsync(y => y.TenantId == tenantId && y.IsCurrent, ct);
        var term = await _db.Set<Term>().FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IsCurrent, ct);
        return new CurrentCalendarDto(year is null ? null : await GetYearAsync(tenantId, year.Id, ct), term is null ? null : ToDto(term));
    }

    public async Task<CalendarYearDto> CreateYearAsync(long tenantId, long userId, CreateAcademicYearRequest req, CancellationToken ct = default)
    {
        var name = req.Name.Trim();
        var (start, end) = (req.StartDate.Date, req.EndDate.Date);
        await EnsureYearIsFreeAsync(tenantId, null, name, start, end, ct);

        var id = await _db.InTransactionAsync(async () =>
        {
            var hasCurrent = await _db.Set<AcademicYear>().AnyAsync(y => y.TenantId == tenantId && y.IsCurrent, ct);
            var year = new AcademicYear { TenantId = tenantId, Name = name, StartDate = start, EndDate = end, CreatedBy = userId };
            _db.Set<AcademicYear>().Add(year);
            await SaveAsync($"Academic year {name} already exists", ct);

            // The first year a school adds becomes its current year.
            if (req.IsCurrent || !hasCurrent) await MakeYearCurrentAsync(tenantId, year.Id, ct);
            return year.Id;
        }, ct);

        return await GetYearAsync(tenantId, id, ct);
    }

    public async Task<CalendarYearDto> UpdateYearAsync(long tenantId, long userId, long yearId, UpdateAcademicYearRequest req, CancellationToken ct = default)
    {
        var year = await FindYearAsync(tenantId, yearId, ct);
        var name = req.Name.Trim();
        var (start, end) = (req.StartDate.Date, req.EndDate.Date);
        await EnsureYearIsFreeAsync(tenantId, yearId, name, start, end, ct);

        var outside = await _db.Set<Term>().FirstOrDefaultAsync(t => t.TenantId == tenantId && t.AcademicYearId == yearId && (t.StartDate < start || t.EndDate > end), ct);
        if (outside is not null)
            throw new InvalidOperationException($"{outside.Name} ({outside.StartDate:yyyy-MM-dd} to {outside.EndDate:yyyy-MM-dd}) would fall outside these dates. Change the term first.");

        year.Name = name;
        year.StartDate = start;
        year.EndDate = end;
        year.UpdatedBy = userId;
        await SaveAsync($"Academic year {name} already exists", ct);
        return await GetYearAsync(tenantId, yearId, ct);
    }

    public async Task<CalendarYearDto> SetCurrentYearAsync(long tenantId, long userId, long yearId, CancellationToken ct = default)
    {
        await FindYearAsync(tenantId, yearId, ct);
        await _db.InTransactionAsync(async () => { await MakeYearCurrentAsync(tenantId, yearId, ct); return true; }, ct);
        return await GetYearAsync(tenantId, yearId, ct);
    }

    public async Task DeleteYearAsync(long tenantId, long userId, long yearId, CancellationToken ct = default)
    {
        var year = await FindYearAsync(tenantId, yearId, ct);
        if (await _db.Set<StudentEnrolment>().AnyAsync(e => e.TenantId == tenantId && e.AcademicYearId == yearId, ct))
            throw new InvalidOperationException($"Students have been enrolled in {year.Name}, so it cannot be deleted.");
        if (await _db.Set<Grade>().AnyAsync(g => g.TenantId == tenantId && g.AcademicYearId == yearId, ct))
            throw new InvalidOperationException($"Delete the grades of {year.Name} first.");
        if (await _db.Set<Term>().AnyAsync(t => t.TenantId == tenantId && t.AcademicYearId == yearId, ct))
            throw new InvalidOperationException($"Delete the terms of {year.Name} first.");

        year.IsCurrent = false;
        year.DeletedBy = userId;
        _db.Set<AcademicYear>().Remove(year); // soft delete in SaveChanges
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CalendarTermDto> CreateTermAsync(long tenantId, long userId, long yearId, CreateTermRequest req, CancellationToken ct = default)
    {
        var year = await FindYearAsync(tenantId, yearId, ct);
        var name = req.Name.Trim();
        var (start, end) = (req.StartDate.Date, req.EndDate.Date);
        await EnsureTermFitsAsync(tenantId, year, null, req.TermNumber, start, end, ct);

        var id = await _db.InTransactionAsync(async () =>
        {
            var term = new Term { TenantId = tenantId, AcademicYearId = yearId, Name = name, TermNumber = req.TermNumber, StartDate = start, EndDate = end, CreatedBy = userId };
            _db.Set<Term>().Add(term);
            await SaveAsync($"Term {req.TermNumber} already exists in {year.Name}", ct);
            if (req.IsCurrent) await MakeTermCurrentAsync(tenantId, term.Id, ct);
            return term.Id;
        }, ct);

        return ToDto(await FindTermAsync(tenantId, id, ct));
    }

    public async Task<CalendarTermDto> UpdateTermAsync(long tenantId, long userId, long termId, UpdateTermRequest req, CancellationToken ct = default)
    {
        var term = await FindTermAsync(tenantId, termId, ct);
        var year = await FindYearAsync(tenantId, term.AcademicYearId, ct);
        var (start, end) = (req.StartDate.Date, req.EndDate.Date);
        await EnsureTermFitsAsync(tenantId, year, termId, req.TermNumber, start, end, ct);

        term.Name = req.Name.Trim();
        term.TermNumber = req.TermNumber;
        term.StartDate = start;
        term.EndDate = end;
        term.UpdatedBy = userId;
        await SaveAsync($"Term {req.TermNumber} already exists in {year.Name}", ct);
        return ToDto(term);
    }

    public async Task<CalendarTermDto> SetCurrentTermAsync(long tenantId, long userId, long termId, CancellationToken ct = default)
    {
        await FindTermAsync(tenantId, termId, ct);
        await _db.InTransactionAsync(async () => { await MakeTermCurrentAsync(tenantId, termId, ct); return true; }, ct);
        return ToDto(await FindTermAsync(tenantId, termId, ct));
    }

    public async Task DeleteTermAsync(long tenantId, long userId, long termId, CancellationToken ct = default)
    {
        var term = await FindTermAsync(tenantId, termId, ct);
        if (await _db.Set<StudentEnrolment>().AnyAsync(e => e.TenantId == tenantId && e.TermId == termId, ct))
            throw new InvalidOperationException($"Enrolments refer to {term.Name}, so it cannot be deleted.");

        term.IsCurrent = false;
        term.DeletedBy = userId;
        _db.Set<Term>().Remove(term);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CalendarYearDto> ReplaceTermsAsync(long tenantId, long userId, long yearId, IReadOnlyList<CreateTermRequest> terms, CancellationToken ct = default)
    {
        var year = await FindYearAsync(tenantId, yearId, ct);
        var wanted = terms.Select(t => new { t.Name, t.TermNumber, Start = t.StartDate.Date, End = t.EndDate.Date, t.IsCurrent }).OrderBy(t => t.Start).ToList();

        if (wanted.Select(t => t.TermNumber).Distinct().Count() != wanted.Count)
            throw new InvalidOperationException("Each term needs a different term number.");
        foreach (var t in wanted)
        {
            if (t.End <= t.Start) throw new InvalidOperationException($"{t.Name} must end after it starts.");
            if (t.Start < year.StartDate || t.End > year.EndDate)
                throw new InvalidOperationException($"{t.Name} must fall within {year.Name} ({year.StartDate:yyyy-MM-dd} to {year.EndDate:yyyy-MM-dd}).");
        }
        for (var i = 1; i < wanted.Count; i++)
            if (wanted[i].Start <= wanted[i - 1].End)
                throw new InvalidOperationException($"{wanted[i].Name} overlaps {wanted[i - 1].Name}.");

        await _db.InTransactionAsync(async () =>
        {
            var existing = await _db.Set<Term>().Where(t => t.TenantId == tenantId && t.AcademicYearId == yearId).ToListAsync(ct);
            var numbers = wanted.Select(t => t.TermNumber).ToHashSet();

            foreach (var removed in existing.Where(t => !numbers.Contains(t.TermNumber)))
            {
                if (await _db.Set<StudentEnrolment>().AnyAsync(e => e.TenantId == tenantId && e.TermId == removed.Id, ct))
                    throw new InvalidOperationException($"Enrolments refer to {removed.Name}, so it cannot be removed.");
                removed.IsCurrent = false;
                _db.Set<Term>().Remove(removed);
            }

            foreach (var t in wanted)
            {
                var term = existing.FirstOrDefault(e => e.TermNumber == t.TermNumber);
                if (term is null)
                {
                    term = new Term { TenantId = tenantId, AcademicYearId = yearId, TermNumber = t.TermNumber, CreatedBy = userId };
                    _db.Set<Term>().Add(term);
                }
                term.Name = t.Name.Trim();
                term.StartDate = t.Start;
                term.EndDate = t.End;
                term.UpdatedBy = userId;
            }
            await _db.SaveChangesAsync(ct);

            var current = wanted.FirstOrDefault(t => t.IsCurrent);
            if (current is not null)
            {
                var currentId = await _db.Set<Term>().Where(t => t.TenantId == tenantId && t.AcademicYearId == yearId && t.TermNumber == current.TermNumber).Select(t => t.Id).SingleAsync(ct);
                await MakeTermCurrentAsync(tenantId, currentId, ct);
            }
            return true;
        }, ct);

        return await GetYearAsync(tenantId, yearId, ct);
    }

    // ---- helpers ------------------------------------------------------------------------

    private async Task EnsureYearIsFreeAsync(long tenantId, long? exceptId, string name, DateTime start, DateTime end, CancellationToken ct)
    {
        if (await _db.Set<AcademicYear>().AnyAsync(y => y.TenantId == tenantId && y.Id != exceptId && y.Name == name, ct))
            throw new InvalidOperationException($"Academic year {name} already exists.");

        var overlap = await _db.Set<AcademicYear>().FirstOrDefaultAsync(y => y.TenantId == tenantId && y.Id != exceptId && y.StartDate <= end && start <= y.EndDate, ct);
        if (overlap is not null)
            throw new InvalidOperationException($"These dates overlap academic year {overlap.Name} ({overlap.StartDate:yyyy-MM-dd} to {overlap.EndDate:yyyy-MM-dd}).");
    }

    private async Task EnsureTermFitsAsync(long tenantId, AcademicYear year, long? exceptId, int termNumber, DateTime start, DateTime end, CancellationToken ct)
    {
        if (start < year.StartDate || end > year.EndDate)
            throw new InvalidOperationException($"Term dates must fall within {year.Name} ({year.StartDate:yyyy-MM-dd} to {year.EndDate:yyyy-MM-dd}).");

        if (await _db.Set<Term>().AnyAsync(t => t.TenantId == tenantId && t.AcademicYearId == year.Id && t.Id != exceptId && t.TermNumber == termNumber, ct))
            throw new InvalidOperationException($"Term {termNumber} already exists in {year.Name}.");

        var overlap = await _db.Set<Term>().FirstOrDefaultAsync(t => t.TenantId == tenantId && t.AcademicYearId == year.Id && t.Id != exceptId && t.StartDate <= end && start <= t.EndDate, ct);
        if (overlap is not null)
            throw new InvalidOperationException($"These dates overlap {overlap.Name} ({overlap.StartDate:yyyy-MM-dd} to {overlap.EndDate:yyyy-MM-dd}).");
    }

    // Callers run these inside a transaction. The old current row is saved first: the
    // one-current partial unique index is checked per statement, so setting the new row
    // before clearing the old one would fail.
    private async Task MakeYearCurrentAsync(long tenantId, long yearId, CancellationToken ct)
    {
        var others = await _db.Set<AcademicYear>().Where(y => y.TenantId == tenantId && y.IsCurrent && y.Id != yearId).ToListAsync(ct);
        others.ForEach(y => y.IsCurrent = false);
        // The current term must belong to the current year.
        var otherTerms = await _db.Set<Term>().Where(t => t.TenantId == tenantId && t.IsCurrent && t.AcademicYearId != yearId).ToListAsync(ct);
        otherTerms.ForEach(t => t.IsCurrent = false);
        await _db.SaveChangesAsync(ct);

        var year = await _db.Set<AcademicYear>().SingleAsync(y => y.TenantId == tenantId && y.Id == yearId, ct);
        year.IsCurrent = true;
        await _db.SaveChangesAsync(ct);
    }

    private async Task MakeTermCurrentAsync(long tenantId, long termId, CancellationToken ct)
    {
        var term = await _db.Set<Term>().SingleAsync(t => t.TenantId == tenantId && t.Id == termId, ct);
        await MakeYearCurrentAsync(tenantId, term.AcademicYearId, ct);

        var others = await _db.Set<Term>().Where(t => t.TenantId == tenantId && t.IsCurrent && t.Id != termId).ToListAsync(ct);
        others.ForEach(t => t.IsCurrent = false);
        await _db.SaveChangesAsync(ct);

        term.IsCurrent = true;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AcademicYear> FindYearAsync(long tenantId, long yearId, CancellationToken ct) =>
        await _db.Set<AcademicYear>().FirstOrDefaultAsync(y => y.TenantId == tenantId && y.Id == yearId, ct)
        ?? throw new InvalidOperationException("Academic year not found");

    private async Task<Term> FindTermAsync(long tenantId, long termId, CancellationToken ct) =>
        await _db.Set<Term>().FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == termId, ct)
        ?? throw new InvalidOperationException("Term not found");

    // A concurrent request can still take a name between the check and the insert; the
    // unique index then refuses it, and the client gets the same conflict message.
    private async Task SaveAsync(string conflictMessage, CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (DatabaseErrors.IsUniqueViolation(ex)) { throw new InvalidOperationException(conflictMessage.TrimEnd('.') + "."); }
    }

    private static CalendarYearDto ToDto(AcademicYear y, IEnumerable<Term> terms) =>
        new(y.Id, y.Name, y.StartDate, y.EndDate, y.IsCurrent, terms.OrderBy(t => t.TermNumber).Select(ToDto).ToList());

    private static CalendarTermDto ToDto(Term t) => new(t.Id, t.AcademicYearId, t.Name, t.TermNumber, t.StartDate, t.EndDate, t.IsCurrent);
}
