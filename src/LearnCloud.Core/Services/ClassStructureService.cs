using LearnCloud.Core.DTOs;
using LearnCloud.MultiTenancy.Context;

namespace LearnCloud.Core.Services;

public interface IClassStructureService
{
    /// <summary>Grades of a year with their streams; the current year when none is given.</summary>
    Task<List<GradeDto>> ListGradesAsync(long tenantId, long? academicYearId, CancellationToken ct = default);
    Task<GradeDto> GetGradeAsync(long tenantId, long gradeId, CancellationToken ct = default);
    Task<GradeDto> CreateGradeAsync(long tenantId, long userId, CreateGradeRequest req, CancellationToken ct = default);
    Task<GradeDto> UpdateGradeAsync(long tenantId, long userId, long gradeId, UpdateGradeRequest req, CancellationToken ct = default);
    Task DeleteGradeAsync(long tenantId, long userId, long gradeId, CancellationToken ct = default);

    Task<StreamDto> CreateStreamAsync(long tenantId, long userId, long gradeId, CreateStreamRequest req, CancellationToken ct = default);
    Task<StreamDto> UpdateStreamAsync(long tenantId, long userId, long streamId, UpdateStreamRequest req, CancellationToken ct = default);
    Task DeleteStreamAsync(long tenantId, long userId, long streamId, CancellationToken ct = default);
}

public class ClassStructureService : IClassStructureService
{
    private readonly LearnCloudDbContext _db;

    public ClassStructureService(LearnCloudDbContext db) => _db = db;

    public async Task<List<GradeDto>> ListGradesAsync(long tenantId, long? academicYearId, CancellationToken ct = default)
    {
        var yearId = academicYearId
            ?? await _db.Set<AcademicYear>().Where(y => y.TenantId == tenantId && y.IsCurrent).Select(y => (long?)y.Id).FirstOrDefaultAsync(ct);
        if (yearId is null) return new List<GradeDto>();

        var grades = await _db.Set<Grade>().Where(g => g.TenantId == tenantId && g.AcademicYearId == yearId)
            .OrderBy(g => g.LevelOrder).ThenBy(g => g.Name).ToListAsync(ct);
        return await ToDtosAsync(tenantId, grades, ct);
    }

    public async Task<GradeDto> GetGradeAsync(long tenantId, long gradeId, CancellationToken ct = default) =>
        (await ToDtosAsync(tenantId, new List<Grade> { await FindGradeAsync(tenantId, gradeId, ct) }, ct)).Single();

    public async Task<GradeDto> CreateGradeAsync(long tenantId, long userId, CreateGradeRequest req, CancellationToken ct = default)
    {
        var year = await _db.Set<AcademicYear>().FirstOrDefaultAsync(y => y.TenantId == tenantId && y.Id == req.AcademicYearId, ct)
            ?? throw new InvalidOperationException("Academic year not found");
        var code = req.Code.Trim().ToUpperInvariant();
        if (await _db.Set<Grade>().AnyAsync(g => g.TenantId == tenantId && g.AcademicYearId == year.Id && g.Code == code, ct))
            throw new InvalidOperationException($"Grade code {code} already exists in {year.Name}.");

        var grade = new Grade { TenantId = tenantId, AcademicYearId = year.Id, Name = req.Name.Trim(), Code = code, LevelOrder = req.LevelOrder, IsActive = true, CreatedBy = userId };
        _db.Set<Grade>().Add(grade);
        await SaveAsync($"Grade code {code} already exists in {year.Name}.", ct);
        return await GetGradeAsync(tenantId, grade.Id, ct);
    }

    public async Task<GradeDto> UpdateGradeAsync(long tenantId, long userId, long gradeId, UpdateGradeRequest req, CancellationToken ct = default)
    {
        var grade = await FindGradeAsync(tenantId, gradeId, ct);
        var code = req.Code.Trim().ToUpperInvariant();
        if (await _db.Set<Grade>().AnyAsync(g => g.TenantId == tenantId && g.AcademicYearId == grade.AcademicYearId && g.Id != gradeId && g.Code == code, ct))
            throw new InvalidOperationException($"Grade code {code} already exists in this academic year.");

        grade.Name = req.Name.Trim();
        grade.Code = code;
        grade.LevelOrder = req.LevelOrder;
        grade.IsActive = req.IsActive;
        grade.UpdatedBy = userId;
        await SaveAsync($"Grade code {code} already exists in this academic year.", ct);
        return await GetGradeAsync(tenantId, gradeId, ct);
    }

    public async Task DeleteGradeAsync(long tenantId, long userId, long gradeId, CancellationToken ct = default)
    {
        var grade = await FindGradeAsync(tenantId, gradeId, ct);
        if (await _db.Set<ClassStream>().AnyAsync(s => s.TenantId == tenantId && s.GradeId == gradeId, ct))
            throw new InvalidOperationException($"Delete the streams of {grade.Name} first.");
        // Enrolment history keeps pointing at the grade, so a grade that was ever used stays.
        if (await _db.Set<StudentEnrolment>().AnyAsync(e => e.TenantId == tenantId && e.GradeId == gradeId, ct)
            || await _db.Set<Student>().AnyAsync(s => s.TenantId == tenantId && s.GradeId == gradeId, ct))
            throw new InvalidOperationException($"Students have been enrolled in {grade.Name}, so it cannot be deleted. Mark it inactive instead.");
        if (await _db.Set<SubjectGradeLink>().AnyAsync(l => l.TenantId == tenantId && l.GradeId == gradeId, ct))
            throw new InvalidOperationException($"Remove the subjects assigned to {grade.Name} first.");

        grade.DeletedBy = userId;
        _db.Set<Grade>().Remove(grade);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<StreamDto> CreateStreamAsync(long tenantId, long userId, long gradeId, CreateStreamRequest req, CancellationToken ct = default)
    {
        var grade = await FindGradeAsync(tenantId, gradeId, ct);
        var name = req.Name.Trim();
        if (await _db.Set<ClassStream>().AnyAsync(s => s.TenantId == tenantId && s.GradeId == gradeId && s.Name == name, ct))
            throw new InvalidOperationException($"Stream {name} already exists in {grade.Name}.");

        var stream = new ClassStream { TenantId = tenantId, GradeId = gradeId, AcademicYearId = grade.AcademicYearId, Name = name, Capacity = req.Capacity, CreatedBy = userId };
        _db.Set<ClassStream>().Add(stream);
        await SaveAsync($"Stream {name} already exists in {grade.Name}.", ct);
        return new StreamDto(stream.Id, gradeId, stream.Name, $"{grade.Name} {stream.Name}", stream.Capacity, 0);
    }

    public async Task<StreamDto> UpdateStreamAsync(long tenantId, long userId, long streamId, UpdateStreamRequest req, CancellationToken ct = default)
    {
        var stream = await FindStreamAsync(tenantId, streamId, ct);
        var grade = await FindGradeAsync(tenantId, stream.GradeId, ct);
        var name = req.Name.Trim();
        if (await _db.Set<ClassStream>().AnyAsync(s => s.TenantId == tenantId && s.GradeId == stream.GradeId && s.Id != streamId && s.Name == name, ct))
            throw new InvalidOperationException($"Stream {name} already exists in {grade.Name}.");

        var enrolled = await CountEnrolledAsync(tenantId, streamId, ct);
        if (req.Capacity < enrolled)
            throw new InvalidOperationException($"{enrolled} students are enrolled in {grade.Name} {stream.Name}; the capacity cannot be lower than that.");

        stream.Name = name;
        stream.Capacity = req.Capacity;
        stream.UpdatedBy = userId;
        await SaveAsync($"Stream {name} already exists in {grade.Name}.", ct);
        return new StreamDto(stream.Id, stream.GradeId, stream.Name, $"{grade.Name} {stream.Name}", stream.Capacity, enrolled);
    }

    public async Task DeleteStreamAsync(long tenantId, long userId, long streamId, CancellationToken ct = default)
    {
        var stream = await FindStreamAsync(tenantId, streamId, ct);
        if (await _db.Set<StudentEnrolment>().AnyAsync(e => e.TenantId == tenantId && e.StreamId == streamId, ct)
            || await _db.Set<Student>().AnyAsync(s => s.TenantId == tenantId && s.StreamId == streamId, ct))
            throw new InvalidOperationException($"Students have been enrolled in stream {stream.Name}, so it cannot be deleted.");

        stream.DeletedBy = userId;
        _db.Set<ClassStream>().Remove(stream);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Students whose current enrolment is in the stream.</summary>
    internal static Task<int> CountEnrolled(LearnCloudDbContext db, long tenantId, long streamId, CancellationToken ct) =>
        db.Set<StudentEnrolment>().CountAsync(e => e.TenantId == tenantId && e.StreamId == streamId && e.IsCurrent, ct);

    private Task<int> CountEnrolledAsync(long tenantId, long streamId, CancellationToken ct) => CountEnrolled(_db, tenantId, streamId, ct);

    private async Task<List<GradeDto>> ToDtosAsync(long tenantId, List<Grade> grades, CancellationToken ct)
    {
        var gradeIds = grades.Select(g => g.Id).ToList();
        var streams = await _db.Set<ClassStream>().Where(s => s.TenantId == tenantId && gradeIds.Contains(s.GradeId)).OrderBy(s => s.Name).ToListAsync(ct);
        var streamIds = streams.Select(s => s.Id).ToList();
        var enrolled = await _db.Set<StudentEnrolment>()
            .Where(e => e.TenantId == tenantId && e.IsCurrent && streamIds.Contains(e.StreamId))
            .GroupBy(e => e.StreamId).Select(g => new { StreamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StreamId, x => x.Count, ct);

        return grades.Select(g => new GradeDto(g.Id, g.AcademicYearId, g.Name, g.Code, g.LevelOrder, g.IsActive,
            streams.Where(s => s.GradeId == g.Id)
                .Select(s => new StreamDto(s.Id, g.Id, s.Name, $"{g.Name} {s.Name}", s.Capacity, enrolled.GetValueOrDefault(s.Id)))
                .ToList())).ToList();
    }

    private async Task<Grade> FindGradeAsync(long tenantId, long gradeId, CancellationToken ct) =>
        await _db.Set<Grade>().FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == gradeId, ct)
        ?? throw new InvalidOperationException("Grade not found");

    private async Task<ClassStream> FindStreamAsync(long tenantId, long streamId, CancellationToken ct) =>
        await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == streamId, ct)
        ?? throw new InvalidOperationException("Stream not found");

    private async Task SaveAsync(string conflictMessage, CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (DatabaseErrors.IsUniqueViolation(ex)) { throw new InvalidOperationException(conflictMessage); }
    }
}
