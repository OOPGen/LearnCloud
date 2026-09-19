using LearnCloud.Core.DTOs;
using LearnCloud.Infrastructure.Text;
using LearnCloud.MultiTenancy.Context;

namespace LearnCloud.Core.Services;

// Students, their enrolments and their guardian links.
//
// Enrolment model (other modules rely on it):
// - A student has at most one enrolment with IsCurrent = true: the class they are in now.
//   Attendance registers and fee invoicing select students through it.
// - Student.GradeId, StreamId and AcademicYearId mirror the current enrolment (or the last
//   one after the student leaves), and CurrentEnrolmentId points at it.
// - Changing class, moving into a later year or readmission closes the current enrolment
//   (IsCurrent = false, ExitDate) and adds a new one, so the history stays complete.
// - Leaving (withdrawn, transferred out, graduated) closes the enrolment with that status
//   and leaves the student with no current enrolment.
public interface IStudentRecordsService
{
    Task<PagedResult<StudentListItemDto>> ListAsync(long tenantId, StudentListRequest req, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(long tenantId, StudentListRequest req, CancellationToken ct = default);
    Task<StudentDetailDto> GetAsync(long tenantId, long studentId, CancellationToken ct = default);
    Task<StudentDetailDto> CreateAsync(long tenantId, long userId, CreateStudentRequest req, CancellationToken ct = default);
    Task<StudentDetailDto> UpdateAsync(long tenantId, long userId, long studentId, UpdateStudentRequest req, CancellationToken ct = default);
    Task DeleteAsync(long tenantId, long userId, long studentId, CancellationToken ct = default);

    Task<StudentDetailDto> ChangeEnrolmentAsync(long tenantId, long userId, long studentId, ChangeEnrolmentRequest req, CancellationToken ct = default);
    Task<StudentDetailDto> ExitAsync(long tenantId, long userId, long studentId, ExitStudentRequest req, CancellationToken ct = default);

    Task<List<StudentGuardianDto>> ListGuardiansAsync(long tenantId, long studentId, CancellationToken ct = default);
    Task<StudentGuardianDto> LinkGuardianAsync(long tenantId, long userId, long studentId, LinkGuardianRequest req, CancellationToken ct = default);
    Task<StudentGuardianDto> UpdateGuardianLinkAsync(long tenantId, long userId, long studentId, long linkId, UpdateGuardianLinkRequest req, CancellationToken ct = default);
    Task UnlinkGuardianAsync(long tenantId, long userId, long studentId, long linkId, CancellationToken ct = default);
}

public class StudentRecordsService : IStudentRecordsService
{
    private const int MaxExportRows = 10_000;
    private readonly LearnCloudDbContext _db;

    public StudentRecordsService(LearnCloudDbContext db) => _db = db;

    // ---- list and export ----------------------------------------------------------------

    public async Task<PagedResult<StudentListItemDto>> ListAsync(long tenantId, StudentListRequest req, CancellationToken ct = default)
    {
        var query = FilteredQuery(tenantId, req);
        var total = await query.CountAsync(ct);
        var rows = await Sorted(query, req).Skip((req.Page - 1) * req.PageSize).Take(req.PageSize).ToListAsync(ct);
        var items = await ToListItemsAsync(tenantId, rows, ct);
        return new PagedResult<StudentListItemDto>(items, total, req.Page, req.PageSize, (int)Math.Ceiling(total / (double)req.PageSize));
    }

    public async Task<byte[]> ExportCsvAsync(long tenantId, StudentListRequest req, CancellationToken ct = default)
    {
        var rows = await Sorted(FilteredQuery(tenantId, req), req).Take(MaxExportRows).ToListAsync(ct);
        var items = await ToListItemsAsync(tenantId, rows, ct);

        var lines = new List<string> { CsvWriter.Row("Student number", "First name", "Last name", "Gender", "Date of birth", "Status", "Academic year", "Grade", "Stream", "Primary guardian", "Guardian phone") };
        lines.AddRange(items.Select(s => CsvWriter.Row(
            s.StudentNumber, s.FirstName, s.LastName, s.Gender, s.Dob?.ToString("yyyy-MM-dd"), s.Status,
            s.AcademicYearName, s.GradeName, s.StreamName, s.PrimaryGuardianName, s.PrimaryGuardianPhone)));
        return CsvWriter.ToUtf8(lines);
    }

    // A class with an object initializer, not a positional record: EF Core cannot translate
    // sorting on members of an object built by a constructor.
    private sealed class StudentRow
    {
        public Student Student { get; init; } = null!;
        public string YearName { get; init; } = "";
        public string GradeName { get; init; } = "";
        public int GradeLevel { get; init; }
        public string StreamName { get; init; } = "";
    }

    private IQueryable<StudentRow> FilteredQuery(long tenantId, StudentListRequest req)
    {
        var students = _db.Set<Student>().Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            students = students.Where(s => s.FirstName.ToLower().Contains(term) || s.LastName.ToLower().Contains(term)
                || (s.FirstName + " " + s.LastName).ToLower().Contains(term) || s.StudentNumber.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(req.Status)) students = students.Where(s => s.Status == req.Status);
        if (req.AcademicYearId.HasValue) students = students.Where(s => s.AcademicYearId == req.AcademicYearId);
        if (req.GradeId.HasValue) students = students.Where(s => s.GradeId == req.GradeId);
        if (req.StreamId.HasValue) students = students.Where(s => s.StreamId == req.StreamId);

        // Grades, streams and years with students are never deleted (the class structure
        // service refuses), so inner joins do not drop students.
        return from s in students
               join g in _db.Set<Grade>().Where(g => g.TenantId == tenantId) on s.GradeId equals g.Id
               join st in _db.Set<ClassStream>().Where(x => x.TenantId == tenantId) on s.StreamId equals st.Id
               join y in _db.Set<AcademicYear>().Where(y => y.TenantId == tenantId) on s.AcademicYearId equals y.Id
               select new StudentRow { Student = s, YearName = y.Name, GradeName = g.Name, GradeLevel = g.LevelOrder, StreamName = st.Name };
    }

    private static IQueryable<StudentRow> Sorted(IQueryable<StudentRow> q, StudentListRequest req) => (req.SortBy ?? "name") switch
    {
        "student_number" => req.SortDesc ? q.OrderByDescending(r => r.Student.StudentNumber) : q.OrderBy(r => r.Student.StudentNumber),
        "created_at" => req.SortDesc ? q.OrderByDescending(r => r.Student.CreatedAt) : q.OrderBy(r => r.Student.CreatedAt),
        "class" => req.SortDesc
            ? q.OrderByDescending(r => r.GradeLevel).ThenByDescending(r => r.StreamName).ThenBy(r => r.Student.LastName)
            : q.OrderBy(r => r.GradeLevel).ThenBy(r => r.StreamName).ThenBy(r => r.Student.LastName),
        _ => req.SortDesc
            ? q.OrderByDescending(r => r.Student.LastName).ThenByDescending(r => r.Student.FirstName)
            : q.OrderBy(r => r.Student.LastName).ThenBy(r => r.Student.FirstName).ThenBy(r => r.Student.Id),
    };

    private async Task<List<StudentListItemDto>> ToListItemsAsync(long tenantId, List<StudentRow> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Student.Id).ToList();
        var primary = await (from l in _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.IsPrimaryContact && ids.Contains(l.StudentId))
                             join g in _db.Set<Guardian>().Where(g => g.TenantId == tenantId) on l.GuardianId equals g.Id
                             select new { l.StudentId, g.FirstName, g.LastName, g.Phone })
                            .ToDictionaryAsync(x => x.StudentId, ct);

        return rows.Select(r =>
        {
            var s = r.Student;
            primary.TryGetValue(s.Id, out var guardian);
            return new StudentListItemDto(s.Id, s.StudentNumber, s.FirstName, s.LastName, s.Gender, s.Dob, s.Status,
                s.AcademicYearId, r.YearName, s.GradeId, r.GradeName, s.StreamId, r.StreamName, s.CurrentEnrolmentId.HasValue,
                guardian is null ? null : $"{guardian.FirstName} {guardian.LastName}", guardian?.Phone, s.CreatedAt);
        }).ToList();
    }

    // ---- one student --------------------------------------------------------------------

    public async Task<StudentDetailDto> GetAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        var s = await FindStudentAsync(tenantId, studentId, ct);

        var enrolments = await _db.Set<StudentEnrolment>().Where(e => e.TenantId == tenantId && e.StudentId == studentId)
            .OrderByDescending(e => e.EnrolmentDate).ThenByDescending(e => e.Id).ToListAsync(ct);
        var yearIds = enrolments.Select(e => e.AcademicYearId).Distinct().ToList();
        var termIds = enrolments.Select(e => e.TermId).Distinct().ToList();
        var gradeIds = enrolments.Select(e => e.GradeId).Distinct().ToList();
        var streamIds = enrolments.Select(e => e.StreamId).Distinct().ToList();
        var years = await _db.Set<AcademicYear>().Where(y => y.TenantId == tenantId && yearIds.Contains(y.Id)).ToDictionaryAsync(y => y.Id, y => y.Name, ct);
        var terms = await _db.Set<Term>().Where(t => t.TenantId == tenantId && termIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var grades = await _db.Set<Grade>().Where(g => g.TenantId == tenantId && gradeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        var streams = await _db.Set<ClassStream>().Where(x => x.TenantId == tenantId && streamIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        string Name(Dictionary<long, string> names, long id) => names.GetValueOrDefault(id, "(deleted)");
        var history = enrolments.Select(e => new EnrolmentDto(e.Id, e.AcademicYearId, Name(years, e.AcademicYearId), e.TermId, Name(terms, e.TermId),
            e.GradeId, Name(grades, e.GradeId), e.StreamId, Name(streams, e.StreamId), e.EnrolmentStatus, e.EnrolmentType, e.EnrolmentDate, e.ExitDate, e.IsCurrent)).ToList();

        var current = history.FirstOrDefault(e => e.IsCurrent);
        var placement = current is null ? null : new PlacementDto(current.Id, current.AcademicYearId, current.AcademicYearName, current.TermId, current.TermName,
            current.GradeId, current.GradeName, current.StreamId, current.StreamName, current.EnrolmentDate);

        return new StudentDetailDto(s.Id, s.StudentNumber, s.FirstName, s.LastName, s.Dob, s.Gender, s.NationalId, s.Status,
            placement, history, await ListGuardiansAsync(tenantId, studentId, ct), s.CreatedAt, s.UpdatedAt);
    }

    public async Task<StudentDetailDto> CreateAsync(long tenantId, long userId, CreateStudentRequest req, CancellationToken ct = default)
    {
        var enrolmentDate = (req.EnrolmentDate ?? DateTime.UtcNow).Date;
        var placement = await ResolvePlacementAsync(tenantId, req.StreamId, req.TermId, enrolmentDate, ct);
        await EnsureCapacityAsync(tenantId, placement, ct);

        var number = string.IsNullOrWhiteSpace(req.StudentNumber) ? null : req.StudentNumber.Trim();
        if (number is not null && await _db.Set<Student>().AnyAsync(s => s.TenantId == tenantId && s.StudentNumber == number, ct))
            throw new InvalidOperationException($"Student number {number} already exists.");
        if (req.Guardian?.GuardianId is long existingGuardianId)
            await FindGuardianAsync(tenantId, existingGuardianId, ct);

        var studentId = await _db.InTransactionAsync(async () =>
        {
            var student = new Student
            {
                TenantId = tenantId,
                StudentNumber = number ?? await NextStudentNumberAsync(tenantId, placement.Year, ct),
                FirstName = req.FirstName.Trim(),
                LastName = req.LastName.Trim(),
                Dob = req.Dob?.Date,
                Gender = req.Gender,
                NationalId = string.IsNullOrWhiteSpace(req.NationalId) ? null : req.NationalId.Trim(),
                Status = EnrolmentValues.StudentActive,
                GradeId = placement.Grade.Id,
                StreamId = placement.Stream.Id,
                AcademicYearId = placement.Year.Id,
                CreatedBy = userId,
            };
            _db.Set<Student>().Add(student);
            await SaveAsync($"Student number {student.StudentNumber} already exists.", ct);

            var enrolment = NewEnrolment(tenantId, userId, student.Id, placement, enrolmentDate, req.EnrolmentType ?? EnrolmentValues.TypeNew, previousId: null);
            _db.Set<StudentEnrolment>().Add(enrolment);
            await _db.SaveChangesAsync(ct);

            student.CurrentEnrolmentId = enrolment.Id;
            await _db.SaveChangesAsync(ct);

            if (req.Guardian is not null) await AddGuardianLinkAsync(tenantId, userId, student, req.Guardian, ct);
            return student.Id;
        }, ct);

        return await GetAsync(tenantId, studentId, ct);
    }

    public async Task<StudentDetailDto> UpdateAsync(long tenantId, long userId, long studentId, UpdateStudentRequest req, CancellationToken ct = default)
    {
        var student = await FindStudentAsync(tenantId, studentId, ct);
        var number = req.StudentNumber.Trim();
        if (await _db.Set<Student>().AnyAsync(s => s.TenantId == tenantId && s.Id != studentId && s.StudentNumber == number, ct))
            throw new InvalidOperationException($"Student number {number} already exists.");

        student.StudentNumber = number;
        student.FirstName = req.FirstName.Trim();
        student.LastName = req.LastName.Trim();
        student.Dob = req.Dob?.Date;
        student.Gender = req.Gender;
        student.NationalId = string.IsNullOrWhiteSpace(req.NationalId) ? null : req.NationalId.Trim();
        student.UpdatedBy = userId;
        await SaveAsync($"Student number {number} already exists.", ct);
        return await GetAsync(tenantId, studentId, ct);
    }

    public async Task DeleteAsync(long tenantId, long userId, long studentId, CancellationToken ct = default)
    {
        await FindStudentAsync(tenantId, studentId, ct);
        await _db.InTransactionAsync(async () =>
        {
            // Soft deletes: the rows stay for audit, and every module's queries stop seeing them.
            var student = await FindStudentAsync(tenantId, studentId, ct);
            var enrolments = await _db.Set<StudentEnrolment>().Where(e => e.TenantId == tenantId && e.StudentId == studentId).ToListAsync(ct);
            var links = await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.StudentId == studentId).ToListAsync(ct);
            _db.Set<GuardianStudentLink>().RemoveRange(links);
            _db.Set<StudentEnrolment>().RemoveRange(enrolments);
            _db.Set<Student>().Remove(student);
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    // ---- enrolment ----------------------------------------------------------------------

    public async Task<StudentDetailDto> ChangeEnrolmentAsync(long tenantId, long userId, long studentId, ChangeEnrolmentRequest req, CancellationToken ct = default)
    {
        var effective = (req.EffectiveDate ?? DateTime.UtcNow).Date;
        var student = await FindStudentAsync(tenantId, studentId, ct);
        var placement = await ResolvePlacementAsync(tenantId, req.StreamId, req.TermId, effective, ct);

        var current = await _db.Set<StudentEnrolment>().FirstOrDefaultAsync(e => e.TenantId == tenantId && e.StudentId == studentId && e.IsCurrent, ct);
        string type;
        string? closingStatus = null;
        if (current is null)
        {
            type = EnrolmentValues.TypeReadmission;
        }
        else
        {
            if (current.StreamId == placement.Stream.Id)
                throw new InvalidOperationException($"{student.FirstName} {student.LastName} is already in {placement.Grade.Name} {placement.Stream.Name}.");

            if (current.AcademicYearId == placement.Year.Id)
            {
                type = EnrolmentValues.TypeContinuing; // class change within the year
            }
            else
            {
                var currentYear = await _db.Set<AcademicYear>().SingleAsync(y => y.TenantId == tenantId && y.Id == current.AcademicYearId, ct);
                if (placement.Year.StartDate <= currentYear.StartDate)
                    throw new InvalidOperationException($"Choose a class in an academic year after {currentYear.Name}.");
                var currentGrade = await _db.Set<Grade>().SingleAsync(g => g.TenantId == tenantId && g.Id == current.GradeId, ct);
                var promoted = placement.Grade.LevelOrder > currentGrade.LevelOrder;
                closingStatus = promoted ? EnrolmentValues.Promoted : EnrolmentValues.Repeated;
                type = promoted ? EnrolmentValues.TypeContinuing : EnrolmentValues.TypeRepeat;
            }
            if (effective < current.EnrolmentDate)
                throw new InvalidOperationException($"The change must take effect on or after {current.EnrolmentDate:yyyy-MM-dd}, when the current enrolment started.");
        }
        await EnsureCapacityAsync(tenantId, placement, ct);

        await _db.InTransactionAsync(async () =>
        {
            var s = await FindStudentAsync(tenantId, studentId, ct);
            var open = await _db.Set<StudentEnrolment>().FirstOrDefaultAsync(e => e.TenantId == tenantId && e.StudentId == studentId && e.IsCurrent, ct);
            long? previousId = open?.Id ?? await _db.Set<StudentEnrolment>().Where(e => e.TenantId == tenantId && e.StudentId == studentId)
                .OrderByDescending(e => e.EnrolmentDate).ThenByDescending(e => e.Id).Select(e => (long?)e.Id).FirstOrDefaultAsync(ct);

            if (open is not null)
            {
                open.IsCurrent = false;
                open.ExitDate = effective;
                if (closingStatus is not null) open.EnrolmentStatus = closingStatus;
                open.UpdatedBy = userId;
                await _db.SaveChangesAsync(ct); // before the insert: one current enrolment per student
            }

            var enrolment = NewEnrolment(tenantId, userId, studentId, placement, effective, type, previousId);
            _db.Set<StudentEnrolment>().Add(enrolment);
            await _db.SaveChangesAsync(ct);

            s.GradeId = placement.Grade.Id;
            s.StreamId = placement.Stream.Id;
            s.AcademicYearId = placement.Year.Id;
            s.CurrentEnrolmentId = enrolment.Id;
            s.Status = EnrolmentValues.StudentActive;
            s.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);

        return await GetAsync(tenantId, studentId, ct);
    }

    public async Task<StudentDetailDto> ExitAsync(long tenantId, long userId, long studentId, ExitStudentRequest req, CancellationToken ct = default)
    {
        var student = await FindStudentAsync(tenantId, studentId, ct);
        var current = await _db.Set<StudentEnrolment>().FirstOrDefaultAsync(e => e.TenantId == tenantId && e.StudentId == studentId && e.IsCurrent, ct)
            ?? throw new InvalidOperationException($"{student.FirstName} {student.LastName} has no current enrolment.");
        var exitDate = (req.ExitDate ?? DateTime.UtcNow).Date;
        if (exitDate < current.EnrolmentDate)
            throw new InvalidOperationException($"The exit date cannot be before {current.EnrolmentDate:yyyy-MM-dd}, when the enrolment started.");

        current.EnrolmentStatus = req.Reason;
        current.ExitDate = exitDate;
        current.IsCurrent = false;
        current.UpdatedBy = userId;
        student.Status = EnrolmentValues.StudentStatusAfterExit(req.Reason);
        student.CurrentEnrolmentId = null;
        student.UpdatedBy = userId;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(tenantId, studentId, ct);
    }

    private sealed record Placement(AcademicYear Year, Term Term, Grade Grade, ClassStream Stream);

    private async Task<Placement> ResolvePlacementAsync(long tenantId, long streamId, long? termId, DateTime onDate, CancellationToken ct)
    {
        var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == streamId, ct)
            ?? throw new InvalidOperationException("Stream not found");
        var grade = await _db.Set<Grade>().SingleAsync(g => g.TenantId == tenantId && g.Id == stream.GradeId, ct);
        var year = await _db.Set<AcademicYear>().SingleAsync(y => y.TenantId == tenantId && y.Id == grade.AcademicYearId, ct);
        if (!grade.IsActive)
            throw new InvalidOperationException($"{grade.Name} is marked inactive and is not taking enrolments.");

        Term? term;
        if (termId.HasValue)
        {
            term = await _db.Set<Term>().FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == termId.Value, ct)
                ?? throw new InvalidOperationException("Term not found");
            if (term.AcademicYearId != year.Id)
                throw new InvalidOperationException($"{term.Name} is not a term of {year.Name}, the academic year of {grade.Name}.");
        }
        else
        {
            // The term that contains the date, else the year's current term, else its first
            // term that has not ended by then, else its last term. The date comes first: a
            // current-term flag left on Term 1 must not file a September enrolment under it.
            var terms = await _db.Set<Term>().Where(t => t.TenantId == tenantId && t.AcademicYearId == year.Id).OrderBy(t => t.StartDate).ToListAsync(ct);
            term = terms.FirstOrDefault(t => t.StartDate <= onDate && onDate <= t.EndDate)
                ?? terms.FirstOrDefault(t => t.IsCurrent)
                ?? terms.FirstOrDefault(t => t.EndDate >= onDate)
                ?? terms.LastOrDefault()
                ?? throw new InvalidOperationException($"Add the terms of {year.Name} before enrolling students.");
        }
        return new Placement(year, term, grade, stream);
    }

    private async Task EnsureCapacityAsync(long tenantId, Placement placement, CancellationToken ct)
    {
        var enrolled = await ClassStructureService.CountEnrolled(_db, tenantId, placement.Stream.Id, ct);
        if (enrolled >= placement.Stream.Capacity)
            throw new InvalidOperationException($"{placement.Grade.Name} {placement.Stream.Name} is full: {enrolled} of {placement.Stream.Capacity} places are taken.");
    }

    private static StudentEnrolment NewEnrolment(long tenantId, long userId, long studentId, Placement p, DateTime date, string type, long? previousId) => new()
    {
        TenantId = tenantId,
        StudentId = studentId,
        AcademicYearId = p.Year.Id,
        TermId = p.Term.Id,
        GradeId = p.Grade.Id,
        StreamId = p.Stream.Id,
        EnrolmentStatus = EnrolmentValues.Enrolled,
        EnrolmentType = type,
        EnrolmentDate = date,
        PreviousEnrolmentId = previousId,
        IsCurrent = true,
        CreatedBy = userId,
    };

    // Numbers look like 2026-0001: the admission year, then a sequence within the school.
    private async Task<string> NextStudentNumberAsync(long tenantId, AcademicYear year, CancellationToken ct)
    {
        var prefix = $"{year.StartDate:yyyy}-";
        var taken = await _db.Set<Student>().Where(s => s.TenantId == tenantId && s.StudentNumber.StartsWith(prefix)).Select(s => s.StudentNumber).ToListAsync(ct);
        var highest = taken.Select(n => int.TryParse(n[prefix.Length..], out var seq) ? seq : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{highest + 1:D4}";
    }

    // ---- guardians ----------------------------------------------------------------------

    public async Task<List<StudentGuardianDto>> ListGuardiansAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        await FindStudentAsync(tenantId, studentId, ct);
        return await (from l in _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.StudentId == studentId)
                      join g in _db.Set<Guardian>().Where(g => g.TenantId == tenantId) on l.GuardianId equals g.Id
                      orderby l.IsPrimaryContact descending, l.CreatedAt
                      select new StudentGuardianDto(l.Id, g.Id, g.FirstName, g.LastName, g.Phone, g.Email, l.RelationshipType,
                          l.IsPrimaryContact, l.IsBillingContact, l.IsEmergencyContact, l.CanPickup)).ToListAsync(ct);
    }

    public async Task<StudentGuardianDto> LinkGuardianAsync(long tenantId, long userId, long studentId, LinkGuardianRequest req, CancellationToken ct = default)
    {
        await FindStudentAsync(tenantId, studentId, ct);
        if (req.GuardianId is long guardianId)
        {
            await FindGuardianAsync(tenantId, guardianId, ct);
            if (await _db.Set<GuardianStudentLink>().AnyAsync(l => l.TenantId == tenantId && l.StudentId == studentId && l.GuardianId == guardianId, ct))
                throw new InvalidOperationException("This guardian is already linked to the student.");
        }

        var linkId = await _db.InTransactionAsync(async () =>
        {
            var student = await FindStudentAsync(tenantId, studentId, ct);
            return await AddGuardianLinkAsync(tenantId, userId, student, req, ct);
        }, ct);
        return (await ListGuardiansAsync(tenantId, studentId, ct)).Single(l => l.LinkId == linkId);
    }

    public async Task<StudentGuardianDto> UpdateGuardianLinkAsync(long tenantId, long userId, long studentId, long linkId, UpdateGuardianLinkRequest req, CancellationToken ct = default)
    {
        await FindLinkAsync(tenantId, studentId, linkId, ct);
        await _db.InTransactionAsync(async () =>
        {
            var link = await FindLinkAsync(tenantId, studentId, linkId, ct);
            if (req.IsPrimaryContact && !link.IsPrimaryContact) await ClearPrimaryContactAsync(tenantId, studentId, ct);
            link.RelationshipType = req.RelationshipType;
            link.IsPrimaryContact = req.IsPrimaryContact;
            link.IsBillingContact = req.IsBillingContact;
            link.IsEmergencyContact = req.IsEmergencyContact;
            link.CanPickup = req.CanPickup;
            link.UpdatedBy = userId;
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return (await ListGuardiansAsync(tenantId, studentId, ct)).Single(l => l.LinkId == linkId);
    }

    public async Task UnlinkGuardianAsync(long tenantId, long userId, long studentId, long linkId, CancellationToken ct = default)
    {
        var link = await FindLinkAsync(tenantId, studentId, linkId, ct);
        link.DeletedBy = userId;
        _db.Set<GuardianStudentLink>().Remove(link);
        await _db.SaveChangesAsync(ct);
    }

    // Runs inside the caller's transaction. Returns the link id.
    private async Task<long> AddGuardianLinkAsync(long tenantId, long userId, Student student, LinkGuardianRequest req, CancellationToken ct)
    {
        long guardianId;
        if (req.GuardianId is long existing)
        {
            guardianId = existing;
        }
        else
        {
            var input = req.NewGuardian!;
            var guardian = GuardianService.NewGuardian(tenantId, userId, input);
            _db.Set<Guardian>().Add(guardian);
            await _db.SaveChangesAsync(ct);
            guardianId = guardian.Id;
        }

        // A student's first guardian is their primary contact.
        var hasLinks = await _db.Set<GuardianStudentLink>().AnyAsync(l => l.TenantId == tenantId && l.StudentId == student.Id, ct);
        var primary = req.IsPrimaryContact || !hasLinks;
        if (primary) await ClearPrimaryContactAsync(tenantId, student.Id, ct);

        var link = new GuardianStudentLink
        {
            TenantId = tenantId,
            GuardianId = guardianId,
            StudentId = student.Id,
            AcademicYearId = student.AcademicYearId,
            RelationshipType = req.RelationshipType,
            IsPrimaryContact = primary,
            IsBillingContact = req.IsBillingContact,
            IsEmergencyContact = req.IsEmergencyContact,
            CanPickup = req.CanPickup,
            CreatedBy = userId,
        };
        _db.Set<GuardianStudentLink>().Add(link);
        await SaveAsync("This guardian is already linked to the student.", ct);
        return link.Id;
    }

    private async Task ClearPrimaryContactAsync(long tenantId, long studentId, CancellationToken ct)
    {
        var primaries = await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.StudentId == studentId && l.IsPrimaryContact).ToListAsync(ct);
        if (primaries.Count == 0) return;
        primaries.ForEach(l => l.IsPrimaryContact = false);
        await _db.SaveChangesAsync(ct); // before setting the new one: one primary contact per student
    }

    // ---- lookups ------------------------------------------------------------------------

    private async Task<Student> FindStudentAsync(long tenantId, long studentId, CancellationToken ct) =>
        await _db.Set<Student>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == studentId, ct)
        ?? throw new InvalidOperationException("Student not found");

    private async Task<Guardian> FindGuardianAsync(long tenantId, long guardianId, CancellationToken ct) =>
        await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == guardianId, ct)
        ?? throw new InvalidOperationException("Guardian not found");

    private async Task<GuardianStudentLink> FindLinkAsync(long tenantId, long studentId, long linkId, CancellationToken ct) =>
        await _db.Set<GuardianStudentLink>().FirstOrDefaultAsync(l => l.TenantId == tenantId && l.StudentId == studentId && l.Id == linkId, ct)
        ?? throw new InvalidOperationException("Guardian link not found");

    private async Task SaveAsync(string conflictMessage, CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (DatabaseErrors.IsUniqueViolation(ex)) { throw new InvalidOperationException(conflictMessage); }
    }
}
