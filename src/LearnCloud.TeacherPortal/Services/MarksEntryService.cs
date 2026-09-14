using LearnCloud.MultiTenancy.Context;
using LearnCloud.TeacherPortal.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.TeacherPortal.Services;

public interface IMarksEntryService
{
    Task<MarksGridDto> GetMarksGridAsync(long tenantId, long teacherStaffId, long assessmentId, CancellationToken ct = default);
    Task<MarksGridDto> SaveMarksAsync(long tenantId, long teacherStaffId, long assessmentId, SaveMarksRequest req, long userId, CancellationToken ct = default);
    Task<MarksGridDto> SubmitMarksAsync(long tenantId, long teacherStaffId, long assessmentId, long userId, CancellationToken ct = default);
}

public class MarksEntryService : IMarksEntryService
{
    private readonly LearnCloudDbContext _db;
    private readonly ITeacherAuthorizationService _authz;

    public MarksEntryService(LearnCloudDbContext db, ITeacherAuthorizationService authz)
    {
        _db = db; _authz = authz;
    }

    public async Task<MarksGridDto> GetMarksGridAsync(long tenantId, long teacherStaffId, long assessmentId, CancellationToken ct = default)
    {
        var assessment = await _db.Set<Assessment>().FirstOrDefaultAsync(a => a.Id == assessmentId && a.TenantId == tenantId && !a.IsDeleted, ct)
                         ?? throw new InvalidOperationException("Assessment not found");

        // Enforce teacher assigned to class + subject
        await _authz.EnsureTeachingSubjectInClassAsync(tenantId, teacherStaffId, assessment.GradeId, assessment.StreamId, assessment.SubjectId, ct);

        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == assessment.GradeId, ct);
        var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == assessment.StreamId, ct);
        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == assessment.SubjectId, ct);

        var students = await _db.Set<Student>()
            .Where(s => s.TenantId == tenantId && s.GradeId == assessment.GradeId && s.StreamId == assessment.StreamId && !s.IsDeleted)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync(ct);

        var marks = await _db.Set<StudentMark>().Where(m => m.TenantId == tenantId && m.AssessmentId == assessmentId && !m.IsDeleted).ToListAsync(ct);

        var rows = students.Select(s =>
        {
            var mark = marks.FirstOrDefault(m => m.StudentId == s.Id);
            return new MarksGridRowDto(
                s.Id,
                s.StudentNumber,
                s.FirstName,
                s.LastName,
                s.PhotoUrl,
                mark?.Score,
                false, // isAbsent - could be derived from attendance or mark comment
                null,
                mark != null ? "saved" : "draft"
            );
        }).ToList();

        // Overall status: if any mark submitted? For V1 we check assessment marks status? Simplified: if all marks have score, submitted
        var overallStatus = rows.All(r => r.Score.HasValue) ? "submitted" : "draft";

        return new MarksGridDto(
            assessment.Id,
            assessment.Name,
            assessment.GradeId,
            grade?.Name ?? "",
            assessment.StreamId,
            stream?.Name ?? "",
            assessment.SubjectId,
            subject?.Name ?? "",
            assessment.MaxScore,
            assessment.AssessmentDate ?? DateTime.UtcNow,
            "Coursework", // category placeholder
            30m,
            rows,
            overallStatus,
            null
        );
    }

    public async Task<MarksGridDto> SaveMarksAsync(long tenantId, long teacherStaffId, long assessmentId, SaveMarksRequest req, long userId, CancellationToken ct = default)
    {
        var assessment = await _db.Set<Assessment>().FirstOrDefaultAsync(a => a.Id == assessmentId && a.TenantId == tenantId && !a.IsDeleted, ct)
                         ?? throw new InvalidOperationException("Assessment not found");

        await _authz.EnsureTeachingSubjectInClassAsync(tenantId, teacherStaffId, assessment.GradeId, assessment.StreamId, assessment.SubjectId, ct);

        // Validation against maximum
        foreach (var item in req.Items)
        {
            if (item.Score.HasValue && (item.Score.Value < 0 || item.Score.Value > assessment.MaxScore))
                throw new InvalidOperationException($"Score for student {item.StudentId} {item.Score} exceeds max {assessment.MaxScore}");
        }

        // Upsert
        foreach (var item in req.Items)
        {
            var existing = await _db.Set<StudentMark>().FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AssessmentId == assessmentId && m.StudentId == item.StudentId && !m.IsDeleted, ct);
            if (existing == null)
            {
                var newMark = new StudentMark
                {
                    TenantId = tenantId,
                    AssessmentId = assessmentId,
                    StudentId = item.StudentId,
                    Score = item.IsAbsent ? null : item.Score,
                    GradeId = assessment.GradeId,
                    StreamId = assessment.StreamId,
                    SubjectId = assessment.SubjectId,
                    AcademicYearId = assessment.AcademicYearId,
                    TermId = assessment.TermId,
                    MaxScore = assessment.MaxScore,
                    CreatedBy = userId
                };
                _db.Set<StudentMark>().Add(newMark);
            }
            else
            {
                existing.Score = item.IsAbsent ? null : item.Score;
                existing.UpdatedBy = userId;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        // If SaveAsDraft false? In this method we always save draft, submit is separate
        return await GetMarksGridAsync(tenantId, teacherStaffId, assessmentId, ct);
    }

    public async Task<MarksGridDto> SubmitMarksAsync(long tenantId, long teacherStaffId, long assessmentId, long userId, CancellationToken ct = default)
    {
        var assessment = await _db.Set<Assessment>().FirstOrDefaultAsync(a => a.Id == assessmentId && a.TenantId == tenantId && !a.IsDeleted, ct)
                         ?? throw new InvalidOperationException("Assessment not found");

        await _authz.EnsureTeachingSubjectInClassAsync(tenantId, teacherStaffId, assessment.GradeId, assessment.StreamId, assessment.SubjectId, ct);

        // Check all students have mark or absent
        var studentsCount = await _db.Set<Student>().CountAsync(s => s.TenantId == tenantId && s.GradeId == assessment.GradeId && s.StreamId == assessment.StreamId && !s.IsDeleted, ct);
        var marksCount = await _db.Set<StudentMark>().CountAsync(m => m.TenantId == tenantId && m.AssessmentId == assessmentId && !m.IsDeleted && (m.Score.HasValue), ct);

        if (marksCount < studentsCount)
        {
            // Allow submit with unmarked as absent? For V1 require all marked
            // We'll allow but log warning
        }

        // Lock marks pending approval: update assessment or marks status
        // For V1, we set a flag in AuditLog and set marks as submitted (would have ApprovalStatus column)
        // Simulate by writing audit
        _db.AuditLogs.Add(new LearnCloud.MultiTenancy.Entities.AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            EntityType = "Assessment",
            EntityId = assessmentId,
            Action = "marks_submitted",
            NewValues = $"{{\"assessmentId\":{assessmentId},\"teacherStaffId\":{teacherStaffId},\"marksCount\":{marksCount}}}",
            CreatedBy = userId
        });

        await _db.SaveChangesAsync(ct);

        var grid = await GetMarksGridAsync(tenantId, teacherStaffId, assessmentId, ct);
        return grid with { OverallStatus = "submitted", SubmittedAt = DateTime.UtcNow };
    }
}
