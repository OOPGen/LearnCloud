using LearnCloud.MultiTenancy.Context;
using LearnCloud.TeacherPortal.DTOs;
using LearnCloud.TeacherPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.TeacherPortal.Services;

public interface IHomeworkService
{
    Task<HomeworkAssignmentDto> CreateAsync(long tenantId, long teacherStaffId, long userId, CreateHomeworkRequest req, CancellationToken ct = default);
    Task<List<HomeworkAssignmentDto>> ListAsync(long tenantId, long teacherStaffId, long? gradeId, long? streamId, CancellationToken ct = default);
    Task<HomeworkAssignmentDto> GetAsync(long tenantId, long teacherStaffId, long assignmentId, CancellationToken ct = default);
    Task<List<HomeworkSubmissionDto>> GetSubmissionsAsync(long tenantId, long teacherStaffId, long assignmentId, CancellationToken ct = default);
    Task<HomeworkSubmissionDto> UpdateFeedbackAsync(long tenantId, long teacherStaffId, long submissionId, UpdateSubmissionFeedbackRequest req, long userId, CancellationToken ct = default);
}

public class HomeworkService : IHomeworkService
{
    private readonly LearnCloudDbContext _db;
    private readonly ITeacherAuthorizationService _authz;

    public HomeworkService(LearnCloudDbContext db, ITeacherAuthorizationService authz) { _db = db; _authz = authz; }

    public async Task<HomeworkAssignmentDto> CreateAsync(long tenantId, long teacherStaffId, long userId, CreateHomeworkRequest req, CancellationToken ct = default)
    {
        await _authz.EnsureAssignedToClassAsync(tenantId, teacherStaffId, req.GradeId, req.StreamId, ct);
        await _authz.EnsureTeachingSubjectInClassAsync(tenantId, teacherStaffId, req.GradeId, req.StreamId, req.SubjectId, ct);

        // For demo, academic year/term from current settings or first grade
        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == req.GradeId, ct);
        var entity = new HomeworkAssignment
        {
            TenantId = tenantId,
            TeacherStaffId = teacherStaffId,
            SubjectId = req.SubjectId,
            GradeId = req.GradeId,
            StreamId = req.StreamId,
            AcademicYearId = 2026,
            TermId = 1,
            Title = req.Title,
            Description = req.Description,
            FileUrl = req.FileUrl,
            FileName = req.FileName,
            DueDate = req.DueDate,
            Status = "active",
            CreatedBy = userId
        };
        _db.Set<HomeworkAssignment>().Add(entity);
        await _db.SaveChangesAsync(ct);

        // Auto-create submissions for each student in class (pending)
        var students = await _db.Set<Student>().Where(s => s.TenantId == tenantId && s.GradeId == req.GradeId && s.StreamId == req.StreamId && !s.IsDeleted).ToListAsync(ct);
        foreach (var student in students)
        {
            _db.Set<HomeworkSubmission>().Add(new HomeworkSubmission
            {
                TenantId = tenantId,
                AssignmentId = entity.Id,
                StudentId = student.Id,
                Status = "pending",
                CreatedBy = userId
            });
        }
        await _db.SaveChangesAsync(ct);

        return await GetAsync(tenantId, teacherStaffId, entity.Id, ct);
    }

    public async Task<List<HomeworkAssignmentDto>> ListAsync(long tenantId, long teacherStaffId, long? gradeId, long? streamId, CancellationToken ct = default)
    {
        var query = _db.Set<HomeworkAssignment>().Where(h => h.TenantId == tenantId && h.TeacherStaffId == teacherStaffId && !h.IsDeleted);
        if (gradeId.HasValue) query = query.Where(h => h.GradeId == gradeId.Value);
        if (streamId.HasValue) query = query.Where(h => h.StreamId == streamId.Value);

        var list = await query.OrderByDescending(h => h.DueDate).ToListAsync(ct);
        var result = new List<HomeworkAssignmentDto>();
        foreach (var h in list)
        {
            var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == h.GradeId, ct);
            var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == h.StreamId, ct);
            var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == h.SubjectId, ct);
            var total = await _db.Set<HomeworkSubmission>().CountAsync(s => s.AssignmentId == h.Id && !s.IsDeleted, ct);
            var submitted = await _db.Set<HomeworkSubmission>().CountAsync(s => s.AssignmentId == h.Id && s.Status == "submitted" && !s.IsDeleted, ct);
            var pending = total - submitted;
            result.Add(new HomeworkAssignmentDto(h.Id, h.Title, h.Description, h.FileUrl, h.FileName, h.DueDate, h.GradeId, grade?.Name ?? "", h.StreamId, stream?.Name ?? "", h.SubjectId, subject?.Name ?? "", h.Status, total, submitted, pending, 0, h.CreatedAt));
        }
        return result;
    }

    public async Task<HomeworkAssignmentDto> GetAsync(long tenantId, long teacherStaffId, long assignmentId, CancellationToken ct = default)
    {
        var h = await _db.Set<HomeworkAssignment>().FirstOrDefaultAsync(x => x.Id == assignmentId && x.TenantId == tenantId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Assignment not found");
        if (h.TeacherStaffId != teacherStaffId) throw new UnauthorizedAccessException("Not your assignment");
        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == h.GradeId, ct);
        var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == h.StreamId, ct);
        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == h.SubjectId, ct);
        var total = await _db.Set<HomeworkSubmission>().CountAsync(s => s.AssignmentId == h.Id && !s.IsDeleted, ct);
        var submitted = await _db.Set<HomeworkSubmission>().CountAsync(s => s.AssignmentId == h.Id && s.Status == "submitted" && !s.IsDeleted, ct);
        return new HomeworkAssignmentDto(h.Id, h.Title, h.Description, h.FileUrl, h.FileName, h.DueDate, h.GradeId, grade?.Name ?? "", h.StreamId, stream?.Name ?? "", h.SubjectId, subject?.Name ?? "", h.Status, total, submitted, total-submitted, 0, h.CreatedAt);
    }

    public async Task<List<HomeworkSubmissionDto>> GetSubmissionsAsync(long tenantId, long teacherStaffId, long assignmentId, CancellationToken ct = default)
    {
        var assignment = await _db.Set<HomeworkAssignment>().FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId && !a.IsDeleted, ct) ?? throw new InvalidOperationException("Assignment not found");
        if (assignment.TeacherStaffId != teacherStaffId) throw new UnauthorizedAccessException("Not your assignment");
        await _authz.EnsureAssignedToClassAsync(tenantId, teacherStaffId, assignment.GradeId, assignment.StreamId, ct);

        var submissions = await _db.Set<HomeworkSubmission>().Where(s => s.AssignmentId == assignmentId && s.TenantId == tenantId && !s.IsDeleted).ToListAsync(ct);
        var result = new List<HomeworkSubmissionDto>();
        foreach (var sub in submissions)
        {
            var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == sub.StudentId, ct);
            result.Add(new HomeworkSubmissionDto(sub.Id, sub.AssignmentId, sub.StudentId, student != null ? $"{student.FirstName} {student.LastName}" : $"Student {sub.StudentId}", student?.StudentNumber ?? "", sub.Status, sub.SubmittedAt, sub.FileUrl, sub.Note));
        }
        return result;
    }

    public async Task<HomeworkSubmissionDto> UpdateFeedbackAsync(long tenantId, long teacherStaffId, long submissionId, UpdateSubmissionFeedbackRequest req, long userId, CancellationToken ct = default)
    {
        var sub = await _db.Set<HomeworkSubmission>().FirstOrDefaultAsync(s => s.Id == submissionId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Submission not found");
        var assignment = await _db.Set<HomeworkAssignment>().FirstOrDefaultAsync(a => a.Id == sub.AssignmentId && a.TenantId == tenantId, ct) ?? throw new InvalidOperationException("Assignment not found");
        if (assignment.TeacherStaffId != teacherStaffId) throw new UnauthorizedAccessException("Not your assignment");
        sub.TeacherFeedback = req.TeacherFeedback;
        sub.Score = req.Score;
        sub.UpdatedBy = userId;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == sub.StudentId, ct);
        return new HomeworkSubmissionDto(sub.Id, sub.AssignmentId, sub.StudentId, student != null ? $"{student.FirstName} {student.LastName}" : "", student?.StudentNumber ?? "", sub.Status, sub.SubmittedAt, sub.FileUrl, sub.Note);
    }
}
