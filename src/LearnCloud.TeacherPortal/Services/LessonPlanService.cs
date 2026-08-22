using LearnCloud.MultiTenancy.Context;
using LearnCloud.TeacherPortal.DTOs;
using LearnCloud.TeacherPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.TeacherPortal.Services;

public interface ILessonPlanService
{
    Task<LessonPlanDto> CreateAsync(long tenantId, long teacherStaffId, long userId, CreateLessonPlanRequest req, CancellationToken ct = default);
    Task<List<LessonPlanDto>> ListAsync(long tenantId, long teacherStaffId, long? gradeId, long? streamId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<LessonPlanDto> GetAsync(long tenantId, long teacherStaffId, long id, CancellationToken ct = default);
    Task<LessonPlanDto> UpdateAsync(long tenantId, long teacherStaffId, long id, CreateLessonPlanRequest req, long userId, CancellationToken ct = default);
}

public class LessonPlanService : ILessonPlanService
{
    private readonly LearnCloudDbContext _db;
    private readonly ITeacherAuthorizationService _authz;

    public LessonPlanService(LearnCloudDbContext db, ITeacherAuthorizationService authz) { _db = db; _authz = authz; }

    public async Task<LessonPlanDto> CreateAsync(long tenantId, long teacherStaffId, long userId, CreateLessonPlanRequest req, CancellationToken ct = default)
    {
        await _authz.EnsureTeachingSubjectInClassAsync(tenantId, teacherStaffId, req.GradeId, req.StreamId, req.SubjectId, ct);

        var entity = new LessonPlan
        {
            TenantId = tenantId,
            TeacherStaffId = teacherStaffId,
            GradeId = req.GradeId,
            StreamId = req.StreamId,
            SubjectId = req.SubjectId,
            AcademicYearId = 2026,
            TermId = 1,
            Date = req.Date,
            Objective = req.Objective,
            Activities = req.Activities,
            Resources = req.Resources,
            Assessment = req.Assessment,
            Reflection = req.Reflection,
            Status = req.Status,
            CreatedBy = userId
        };
        _db.Set<LessonPlan>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(tenantId, teacherStaffId, entity.Id, ct);
    }

    public async Task<List<LessonPlanDto>> ListAsync(long tenantId, long teacherStaffId, long? gradeId, long? streamId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var q = _db.Set<LessonPlan>().Where(lp => lp.TenantId == tenantId && lp.TeacherStaffId == teacherStaffId && !lp.IsDeleted);
        if (gradeId.HasValue) q = q.Where(lp => lp.GradeId == gradeId.Value);
        if (streamId.HasValue) q = q.Where(lp => lp.StreamId == streamId.Value);
        if (from.HasValue) q = q.Where(lp => lp.Date >= from.Value);
        if (to.HasValue) q = q.Where(lp => lp.Date <= to.Value);

        var list = await q.OrderByDescending(lp => lp.Date).ToListAsync(ct);
        var result = new List<LessonPlanDto>();
        foreach (var lp in list)
        {
            var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == lp.GradeId, ct);
            var stream = await _db.Streams.FirstOrDefaultAsync(s => s.Id == lp.StreamId, ct);
            var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == lp.SubjectId, ct);
            result.Add(new LessonPlanDto(lp.Id, lp.GradeId, grade?.Name ?? "", lp.StreamId, stream?.Name ?? "", lp.SubjectId, subject?.Name ?? "", lp.Date, lp.Objective, lp.Activities, lp.Resources, lp.Assessment, lp.Reflection, lp.Status));
        }
        return result;
    }

    public async Task<LessonPlanDto> GetAsync(long tenantId, long teacherStaffId, long id, CancellationToken ct = default)
    {
        var lp = await _db.Set<LessonPlan>().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Lesson plan not found");
        if (lp.TeacherStaffId != teacherStaffId) throw new UnauthorizedAccessException("Not your lesson plan");
        var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == lp.GradeId, ct);
        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.Id == lp.StreamId, ct);
        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == lp.SubjectId, ct);
        return new LessonPlanDto(lp.Id, lp.GradeId, grade?.Name ?? "", lp.StreamId, stream?.Name ?? "", lp.SubjectId, subject?.Name ?? "", lp.Date, lp.Objective, lp.Activities, lp.Resources, lp.Assessment, lp.Reflection, lp.Status);
    }

    public async Task<LessonPlanDto> UpdateAsync(long tenantId, long teacherStaffId, long id, CreateLessonPlanRequest req, long userId, CancellationToken ct = default)
    {
        var lp = await _db.Set<LessonPlan>().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Lesson plan not found");
        if (lp.TeacherStaffId != teacherStaffId) throw new UnauthorizedAccessException("Not your lesson plan");
        await _authz.EnsureTeachingSubjectInClassAsync(tenantId, teacherStaffId, req.GradeId, req.StreamId, req.SubjectId, ct);

        lp.GradeId = req.GradeId;
        lp.StreamId = req.StreamId;
        lp.SubjectId = req.SubjectId;
        lp.Date = req.Date;
        lp.Objective = req.Objective;
        lp.Activities = req.Activities;
        lp.Resources = req.Resources;
        lp.Assessment = req.Assessment;
        lp.Reflection = req.Reflection;
        lp.Status = req.Status;
        lp.UpdatedBy = userId;
        lp.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetAsync(tenantId, teacherStaffId, id, ct);
    }
}
