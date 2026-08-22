using LearnCloud.MultiTenancy.Context;
using LearnCloud.ParentPortal.DTOs;
using LearnCloud.ParentPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.ParentPortal.Services.ParentTeacherMessaging;

public class ParentTeacherMessagingService
{
    private readonly LearnCloudDbContext _db;
    private readonly IParentAuthorizationService _authz;

    public ParentTeacherMessagingService(LearnCloudDbContext db, IParentAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
    }

    public async Task<TeacherMessageDto> SendMessageAsync(long tenantId, long guardianId, long userId, SendMessageToTeacherRequest req, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, req.StudentId, ct);

        // Check school enables parent-teacher messaging
        var settings = await _db.Set<ParentMessagingSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        if (settings != null && !settings.EnableParentTeacherMessaging)
            throw new InvalidOperationException("Parent-teacher messaging is disabled by school");

        // Rate limiting: 5 per hour per guardian per class, 20 per day
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        var oneDayAgo = now.AddDays(-1);

        var recentHour = await _db.Set<ParentTeacherMessage>().CountAsync(m => m.TenantId == tenantId && m.GuardianId == guardianId && m.GradeId == req.GradeId && m.StreamId == req.StreamId && m.CreatedAt >= oneHourAgo && !m.IsDeleted, ct);
        var rateLimitPerHour = settings?.RateLimitPerHour ?? 5;
        if (recentHour >= rateLimitPerHour)
            throw new InvalidOperationException($"Rate limit: max {rateLimitPerHour} messages per hour per class. Please wait.");

        var recentDay = await _db.Set<ParentTeacherMessage>().CountAsync(m => m.TenantId == tenantId && m.GuardianId == guardianId && m.CreatedAt >= oneDayAgo && !m.IsDeleted, ct);
        var rateLimitPerDay = settings?.RateLimitPerDay ?? 20;
        if (recentDay >= rateLimitPerDay)
            throw new InvalidOperationException($"Rate limit: max {rateLimitPerDay} messages per day. Please wait tomorrow.");

        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == req.StudentId && s.TenantId == tenantId, ct);
        var requiresModeration = settings?.RequireModeration ?? false;

        var message = new ParentTeacherMessage
        {
            TenantId = tenantId,
            GuardianId = guardianId,
            StudentId = req.StudentId,
            GradeId = req.GradeId,
            StreamId = req.StreamId,
            SenderUserId = userId,
            RecipientTeacherStaffId = req.RecipientTeacherStaffId,
            Subject = req.Subject,
            Body = req.Body,
            Status = requiresModeration ? "pending" : "sent",
            RequiresModeration = requiresModeration,
            RateLimitKey = $"{guardianId}:{req.GradeId}:{req.StreamId}",
            CreatedBy = userId
        };

        _db.Set<ParentTeacherMessage>().Add(message);
        await _db.SaveChangesAsync(ct);

        return new TeacherMessageDto(
            message.Id,
            message.StudentId,
            student != null ? $"{student.FirstName} {student.LastName}" : "",
            message.Subject,
            message.Body,
            message.Status,
            message.CreatedAt,
            message.ModerationNote,
            message.RequiresModeration
        );
    }

    public async Task<MessageThreadDto> GetThreadAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);

        var messages = await _db.Set<ParentTeacherMessage>()
            .Where(m => m.TenantId == tenantId && m.GuardianId == guardianId && m.StudentId == studentId && !m.IsDeleted)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var dtos = new List<TeacherMessageDto>();
        foreach (var m in messages)
        {
            var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == m.StudentId, ct);
            dtos.Add(new TeacherMessageDto(
                m.Id,
                m.StudentId,
                student != null ? $"{student.FirstName} {student.LastName}" : "",
                m.Subject,
                m.Body,
                m.Status,
                m.CreatedAt,
                m.ModerationNote,
                m.RequiresModeration
            ));
        }

        return new MessageThreadDto(dtos);
    }
}
