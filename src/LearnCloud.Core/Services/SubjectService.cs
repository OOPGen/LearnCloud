using LearnCloud.Core.DTOs;
using LearnCloud.Infrastructure.Text;

using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Context;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Core.Services;

public interface ISubjectService
{
    Task<PagedResult<SubjectDto>> GetListAsync(long tenantId, SubjectListRequest req, CancellationToken ct = default);
    Task<SubjectDto> GetByIdAsync(long tenantId, long id, CancellationToken ct = default);
    Task<SubjectDto> CreateAsync(long tenantId, long userId, CreateSubjectRequest req, CancellationToken ct = default);
    Task<SubjectDto> UpdateAsync(long tenantId, long userId, long id, UpdateSubjectRequest req, CancellationToken ct = default);
    Task DeleteAsync(long tenantId, long userId, long id, CancellationToken ct = default);
    Task<GradeSubjectDto> AssignToGradeAsync(long tenantId, long userId, AssignSubjectToGradeRequest req, CancellationToken ct = default);
    Task<List<GradeSubjectDto>> GetByGradeAsync(long tenantId, long gradeId, long academicYearId, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(long tenantId, SubjectListRequest req, CancellationToken ct = default);
}

public class SubjectService : ISubjectService
{
    private readonly LearnCloudDbContext _db;

    public SubjectService(LearnCloudDbContext db) => _db = db;

    public async Task<PagedResult<SubjectDto>> GetListAsync(long tenantId, SubjectListRequest req, CancellationToken ct = default)
    {
        var query = _db.Set<Subject>().Where(s => s.TenantId == tenantId && !s.IsDeleted);

        // Server-side search: name, code, department
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var search = req.Search.ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(search) || s.Code.ToLower().Contains(search) || (s.Department != null && s.Department.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(req.Department))
            query = query.Where(s => s.Department == req.Department);

        if (req.IsCore.HasValue)
            query = query.Where(s => s.IsCore == req.IsCore.Value);

        if (!string.IsNullOrWhiteSpace(req.Status))
            query = query.Where(s => s.Status == req.Status);

        // Server-side sort
        query = req.SortBy?.ToLower() switch
        {
            "name" => req.SortDesc ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
            "code" => req.SortDesc ? query.OrderByDescending(s => s.Code) : query.OrderBy(s => s.Code),
            "department" => req.SortDesc ? query.OrderByDescending(s => s.Department) : query.OrderBy(s => s.Department),
            "created_at" => req.SortDesc ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
            _ => query.OrderBy(s => s.Name)
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize).Include(s => s.GradeSubjects).ToListAsync(ct);

        var dtos = items.Select(s => new SubjectDto(
            s.Id,
            s.Name,
            s.Code,
            s.Description,
            s.IsCore,
            s.Department,
            s.Status,
            s.GradeSubjects.Count(gs => !gs.IsDeleted),
            s.CreatedAt
        )).ToList();

        return new PagedResult<SubjectDto>(dtos, total, req.Page, req.PageSize, (int)Math.Ceiling(total / (double)req.PageSize));
    }

    public async Task<SubjectDto> GetByIdAsync(long tenantId, long id, CancellationToken ct = default)
    {
        var subject = await _db.Set<Subject>().Where(s => s.TenantId == tenantId && s.Id == id && !s.IsDeleted).Include(s => s.GradeSubjects).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Subject not found");

        return new SubjectDto(
            subject.Id,
            subject.Name,
            subject.Code,
            subject.Description,
            subject.IsCore,
            subject.Department,
            subject.Status,
            subject.GradeSubjects.Count(gs => !gs.IsDeleted),
            subject.CreatedAt
        );
    }

    public async Task<SubjectDto> CreateAsync(long tenantId, long userId, CreateSubjectRequest req, CancellationToken ct = default)
    {
        // Unique per tenant: code
        var exists = await _db.Set<Subject>().AnyAsync(s => s.TenantId == tenantId && s.Code == req.Code && !s.IsDeleted, ct);
        if (exists) throw new InvalidOperationException($"Subject code {req.Code} already exists in this tenant");

        var subject = new Subject
        {
            TenantId = tenantId,
            Name = req.Name,
            Code = req.Code,
            Description = req.Description,
            IsCore = req.IsCore,
            Department = req.Department,
            Status = "active",
            CreatedBy = userId
        };

        _db.Set<Subject>().Add(subject);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(tenantId, subject.Id, ct);
    }

    public async Task<SubjectDto> UpdateAsync(long tenantId, long userId, long id, UpdateSubjectRequest req, CancellationToken ct = default)
    {
        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subject not found");

        var oldValues = System.Text.Json.JsonSerializer.Serialize(subject);

        if (req.Name != null) subject.Name = req.Name;
        if (req.Code != null)
        {
            var exists = await _db.Set<Subject>().AnyAsync(s => s.TenantId == tenantId && s.Code == req.Code && s.Id != id && !s.IsDeleted, ct);
            if (exists) throw new InvalidOperationException($"Subject code {req.Code} already exists");
            subject.Code = req.Code;
        }
        if (req.Description != null) subject.Description = req.Description;
        if (req.IsCore.HasValue) subject.IsCore = req.IsCore.Value;
        if (req.Department != null) subject.Department = req.Department;
        if (req.Status != null) subject.Status = req.Status;

        subject.UpdatedBy = userId;
        subject.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(tenantId, id, ct);
    }

    public async Task DeleteAsync(long tenantId, long userId, long id, CancellationToken ct = default)
    {
        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subject not found");

        // Check if subject is in use in grade_subjects or timetable
        var inUse = await _db.Set<SubjectGradeLink>().AnyAsync(gs => gs.TenantId == tenantId && gs.SubjectId == id && !gs.IsDeleted, ct);
        if (inUse) throw new InvalidOperationException("Cannot delete subject that is assigned to grades - archive instead");

        subject.IsDeleted = true;
        subject.DeletedAt = DateTime.UtcNow;
        subject.DeletedBy = userId;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<GradeSubjectDto> AssignToGradeAsync(long tenantId, long userId, AssignSubjectToGradeRequest req, CancellationToken ct = default)
    {
        var exists = await _db.Set<SubjectGradeLink>().AnyAsync(gs => gs.TenantId == tenantId && gs.GradeId == req.GradeId && gs.SubjectId == req.SubjectId && gs.AcademicYearId == req.AcademicYearId && !gs.IsDeleted, ct);
        if (exists) throw new InvalidOperationException("Subject already assigned to this grade for this academic year");

        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == req.GradeId && g.TenantId == tenantId && !g.IsDeleted, ct) ?? throw new InvalidOperationException("Grade not found");
        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == req.SubjectId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Subject not found");

        var gradeSubject = new SubjectGradeLink
        {
            TenantId = tenantId,
            GradeId = req.GradeId,
            SubjectId = req.SubjectId,
            AcademicYearId = req.AcademicYearId,
            IsCompulsory = req.IsCompulsory,
            CreatedBy = userId
        };

        _db.Set<SubjectGradeLink>().Add(gradeSubject);
        await _db.SaveChangesAsync(ct);

        return new GradeSubjectDto(gradeSubject.Id, grade.Id, grade.Name, grade.Code, subject.Id, subject.Name, req.AcademicYearId, req.IsCompulsory);
    }

    public async Task<List<GradeSubjectDto>> GetByGradeAsync(long tenantId, long gradeId, long academicYearId, CancellationToken ct = default)
    {
        var list = await _db.Set<SubjectGradeLink>().Where(gs => gs.TenantId == tenantId && gs.GradeId == gradeId && gs.AcademicYearId == academicYearId && !gs.IsDeleted).Include(gs => gs.Grade).Include(gs => gs.Subject).ToListAsync(ct);
        return list.Select(gs => new GradeSubjectDto(gs.Id, gs.Grade.Id, gs.Grade.Name, gs.Grade.Code, gs.Subject.Id, gs.Subject.Name, gs.AcademicYearId, gs.IsCompulsory)).ToList();
    }

    public async Task<byte[]> ExportCsvAsync(long tenantId, SubjectListRequest req, CancellationToken ct = default)
    {
        // CsvWriter quotes every value and neutralises spreadsheet formulas; names and
        // descriptions used to be written as they were, so "=HYPERLINK(...)" would run in Excel.
        var result = await GetListAsync(tenantId, req with { Page = 1, PageSize = 1000 }, ct);
        var lines = new List<string> { CsvWriter.Row("Id", "Name", "Code", "Description", "IsCore", "Department", "Status", "GradesOffered", "CreatedAt") };
        lines.AddRange(result.Items.Select(item => CsvWriter.Row(
            item.Id.ToString(), item.Name, item.Code, item.Description, item.IsCore ? "true" : "false", item.Department, item.Status,
            item.GradesOffered.ToString(), item.CreatedAt.ToString("yyyy-MM-dd"))));
        return CsvWriter.ToUtf8(lines);
    }
}
