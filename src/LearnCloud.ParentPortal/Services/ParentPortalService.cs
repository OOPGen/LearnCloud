using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.Auth.Entities;
using LearnCloud.Messaging.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using LearnCloud.ParentPortal.DTOs;
using LearnCloud.ParentPortal.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.ParentPortal.Services;

public interface IParentPortalService
{
    Task<InvitationDto> InviteGuardianAsync(long tenantId, long actorUserId, InviteGuardianRequest req, CancellationToken ct = default);
    Task AcceptInvitationAsync(string token, string email, string newPassword, CancellationToken ct = default);
    Task<ChildrenListDto> GetChildrenAsync(long tenantId, long guardianId, CancellationToken ct = default);
    Task<ChildHomeDto> GetChildHomeAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task<FeeStatementDto> GetStatementAsync(long tenantId, long guardianId, long studentId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<InvoiceHistoryDto>> GetInvoiceHistoryAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task<List<ReceiptDto>> GetReceiptsAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task<List<AttendanceDetailDto>> GetAttendanceDetailAsync(long tenantId, long guardianId, long studentId, long? academicYearId, long? termId, CancellationToken ct = default);
    Task<AttendanceSummaryDto> GetAttendanceSummaryAsync(long tenantId, long guardianId, long studentId, long academicYearId, long termId, CancellationToken ct = default);
    Task<List<ReportCardDto>> GetPublishedReportCardsAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task<ReportCardDetailDto> GetReportCardDetailAsync(long tenantId, long guardianId, long reportCardId, CancellationToken ct = default);
    Task<List<NoticeDto>> GetNoticesAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task<List<HomeworkDto>> GetHomeworkAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default);
    Task<ParentProfileDto> GetProfileAsync(long tenantId, long guardianId, long userId, CancellationToken ct = default);
    Task<ParentProfileDto> UpdateContactPreferencesAsync(long tenantId, long guardianId, long userId, UpdateContactPreferencesRequest req, CancellationToken ct = default);
}

public class ParentPortalService : IParentPortalService
{
    private readonly LearnCloudDbContext _db;
    private readonly IParentAuthorizationService _authz;
    private readonly Fees.Services.FeeCalculationService _calc; // reuse fee calc for statement

    public ParentPortalService(LearnCloudDbContext db, IParentAuthorizationService authz, Fees.Services.FeeCalculationService calc)
    {
        _db = db; _authz = authz; _calc = calc;
    }

    public async Task<InvitationDto> InviteGuardianAsync(long tenantId, long actorUserId, InviteGuardianRequest req, CancellationToken ct = default)
    {
        var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == req.GuardianId && g.TenantId == tenantId && !g.IsDeleted, ct)
                       ?? throw new InvalidOperationException("Guardian not found");

        // Generate token 32 bytes base64url, hash SHA256
        var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).Replace("+","-").Replace("/","_").TrimEnd('=');
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))).ToLower();

        var invitation = new GuardianInvitation
        {
            TenantId = tenantId,
            GuardianId = guardian.Id,
            Email = req.Email,
            Phone = req.Phone,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByUserId = actorUserId,
            InvitationLink = $"https://learncloud.co.zw/parent/accept-invitation?token={rawToken}&email={req.Email}"
        };
        _db.Set<GuardianInvitation>().Add(invitation);
        await _db.SaveChangesAsync(ct);

        // In real app, send email/SMS with rawToken link via IEmailSender/ISmsProvider
        // Never log raw token, only hashed

        return new InvitationDto(invitation.Id, guardian.Id, req.Email, invitation.ExpiresAt, false, null, invitation.InvitationLink);
    }

    public async Task AcceptInvitationAsync(string token, string email, string newPassword, CancellationToken ct = default)
    {
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLower();
        var invitation = await _db.Set<GuardianInvitation>().FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.Email.ToLower() == email.ToLower() && !i.IsDeleted, ct)
                         ?? throw new InvalidOperationException("Invalid or expired invitation");

        if (invitation.IsUsed) throw new InvalidOperationException("Invitation already used");
        if (invitation.ExpiresAt < DateTime.UtcNow) throw new InvalidOperationException("Invitation expired");

        var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == invitation.GuardianId && !g.IsDeleted, ct)
                       ?? throw new InvalidOperationException("Guardian not found");

        // Create user account linked to guardian
        // Use Identity hasher
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Guardian>();
        var passwordHash = hasher.HashPassword(guardian, newPassword);

        // Check if user exists with email
        var existingUser = await _db.Set<User>().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.TenantId == invitation.TenantId && !u.IsDeleted, ct);
        User user;
        if (existingUser == null)
        {
            user = new User
            {
                TenantId = invitation.TenantId,
                Email = email.ToLower(),
                DisplayName = $"{guardian.FirstName} {guardian.LastName}",
                PasswordHash = passwordHash,
                Status = "active",
                EmailVerified = true,
                EmailVerifiedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString(),
                TokenVersion = 1
            };
            _db.Set<User>().Add(user);
            await _db.SaveChangesAsync(ct);

            // Assign PARENT role
            var parentRole = await _db.Set<Role>().FirstOrDefaultAsync(r => r.TenantId == invitation.TenantId && r.Code == "PARENT" && !r.IsDeleted, ct);
            if (parentRole != null)
            {
                _db.Set<UserRole>().Add(new UserRole { TenantId = invitation.TenantId, UserId = user.Id, RoleId = parentRole.Id });
            }
        }
        else
        {
            user = existingUser;
            user.PasswordHash = passwordHash;
            user.EmailVerified = true;
            user.Status = "active";
        }

        guardian.UserId = user.Id;
        invitation.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<ChildrenListDto> GetChildrenAsync(long tenantId, long guardianId, CancellationToken ct = default)
    {
        var studentIds = await _authz.GetStudentIdsForGuardianAsync(tenantId, guardianId, ct);
        var students = await _db.Set<Student>().Where(s => studentIds.Contains(s.Id) && s.TenantId == tenantId && !s.IsDeleted).ToListAsync(ct);

        var children = new List<ChildDto>();
        foreach (var student in students)
        {
            var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == student.GradeId, ct);
            var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == student.StreamId, ct);
            children.Add(new ChildDto(
                student.Id,
                student.StudentNumber,
                student.FirstName,
                student.LastName,
                $"{student.FirstName} {student.LastName}",
                grade?.Name ?? "",
                stream?.Name ?? "",
                student.GradeId,
                student.StreamId,
                student.PhotoUrl,
                student.Status ?? "active"
            ));
        }

        return new ChildrenListDto(children.OrderBy(c => c.FullName).ToList());
    }

    public async Task<ChildHomeDto> GetChildHomeAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);

        // PERFORMANCE FIX C2: Was 10+ sequential queries + N+1 subject lookups in loops
        // Batch subject loading and single queries remain; the Task.WhenAll part was reverted (see below).

        var studentTask = _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId && !s.IsDeleted, ct);
        var student = await studentTask ?? throw new InvalidOperationException("Student not found");

        // Sequential on purpose: one DbContext cannot run queries concurrently, and the
        // earlier Task.WhenAll version threw "a second operation was started on this context".

        var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == student.GradeId, ct);
        var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == student.StreamId, ct);
        var currentTerm = (await _db.Set<Term>().FirstOrDefaultAsync(t => t.IsCurrent && t.TenantId == tenantId, ct)) ?? await _db.Set<Term>().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        var invoices = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == studentId && !i.IsDeleted && i.BalanceDue > 0).ToListAsync(ct);
        var latestReport = await _db.Set<ReportCard>().Where(rc => rc.TenantId == tenantId && rc.StudentId == studentId && rc.Status == "published" && !rc.IsDeleted).OrderByDescending(rc => rc.PublishedAt).FirstOrDefaultAsync(ct);
        var upcomingAssessments = await _db.Set<Assessment>().Where(a => a.TenantId == tenantId && a.GradeId == student.GradeId && a.StreamId == student.StreamId && !a.IsDeleted && a.AssessmentDate >= DateTime.UtcNow.Date && a.AssessmentDate <= DateTime.UtcNow.Date.AddDays(14)).OrderBy(a => a.AssessmentDate).Take(5).ToListAsync(ct);
        var notices = await _db.Set<Messaging.Entities.MessageBatch>().Where(b => b.TenantId == tenantId && !b.IsDeleted).OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync(ct);
        var homework = await _db.Set<TeacherPortal.Entities.HomeworkAssignment>().Where(h => h.TenantId == tenantId && h.GradeId == student.GradeId && h.StreamId == student.StreamId && !h.IsDeleted && h.DueDate >= DateTime.UtcNow.Date).OrderBy(h => h.DueDate).Take(5).ToListAsync(ct);

        var outstanding = invoices.Sum(i => i.BalanceDue);
        var currency = invoices.FirstOrDefault()?.Currency ?? "USD";

        // Attendance - needs currentTerm, so fetch after currentTerm known, but still parallelizable with its own query
        var attRecords = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == studentId && !a.IsDeleted && (currentTerm == null || a.TermId == currentTerm.Id)).ToListAsync(ct);
        var totalDays = attRecords.Count;
        var presentDays = attRecords.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late || r.Status == AttendanceStatus.Excused);
        var attendancePerc = totalDays > 0 ? Math.Round((decimal)presentDays / totalDays * 100, 1) : 0m;
        var isChronic = attendancePerc < 85m && totalDays > 10;

        LatestResultDto? latestResult = null;
        if (latestReport != null)
        {
            var term = await _db.Set<Term>().FirstOrDefaultAsync(t => t.Id == latestReport.TermId, ct);
            latestResult = new LatestResultDto(latestReport.Id, term?.Name ?? $"Term {latestReport.TermId}", latestReport.TotalAverage, latestReport.OverallGradeLetter, latestReport.PublishedAt ?? latestReport.CreatedAt, latestReport.PdfUrl);
        }

        // Batch subject loading for upcoming assessments and homework to avoid N+1
        var subjectIds = upcomingAssessments.Select(a => a.SubjectId).Concat(homework.Select(h => h.SubjectId)).Distinct().ToList();
        var subjectsDict = new Dictionary<long, Subject>();
        if (subjectIds.Count > 0)
        {
            subjectsDict = await _db.Set<Subject>().Where(s => subjectIds.Contains(s.Id) && s.TenantId == tenantId).ToDictionaryAsync(s => s.Id, ct);
        }

        var upcomingDtos = upcomingAssessments.Select(ass =>
        {
            subjectsDict.TryGetValue(ass.SubjectId, out var subj);
            return new UpcomingAssessmentDto(ass.Id, ass.Name, subj?.Name ?? "", ass.AssessmentDate ?? DateTime.UtcNow, (ass.AssessmentDate.HasValue ? (ass.AssessmentDate.Value - DateTime.UtcNow.Date).Days : 0), ass.MaxScore);
        }).ToList();

        var noticeDtos = notices.Select(n => new NoticeDto(n.Id, n.Title, n.Body, n.CreatedAt, "normal", false)).ToList();

        var homeworkDtos = homework.Select(hw =>
        {
            subjectsDict.TryGetValue(hw.SubjectId, out var subj);
            return new HomeworkDto(hw.Id, hw.Title, subj?.Name ?? "", hw.DueDate, (hw.DueDate - DateTime.UtcNow.Date).Days, hw.Status);
        }).ToList();

        return new ChildHomeDto(
            studentId,
            $"{student.FirstName} {student.LastName}",
            grade?.Name ?? "",
            stream?.Name ?? "",
            outstanding,
            currency,
            attendancePerc,
            isChronic,
            latestResult,
            upcomingDtos,
            noticeDtos,
            homeworkDtos
        );
    }

    public async Task<FeeStatementDto> GetStatementAsync(long tenantId, long guardianId, long studentId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);

        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId, ct) ?? throw new InvalidOperationException("Student not found");
        var invoices = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == studentId && !i.IsDeleted).OrderBy(i => i.IssueDate).ToListAsync(ct);
        var payments = await _db.Set<Fees.Entities.Payment>().Where(p => p.TenantId == tenantId && p.StudentId == studentId && !p.IsDeleted && p.Status != Fees.Entities.PaymentStatus.Reversed).OrderBy(p => p.PaymentDate).ToListAsync(ct);
        var credits = await _db.Set<Fees.Entities.LearnerCredit>().Where(c => c.TenantId == tenantId && c.StudentId == studentId && !c.IsDeleted).ToListAsync(ct);

        var lines = new List<FeeStatementLineDto>();
        decimal running = 0m;
        foreach (var inv in invoices)
        {
            if (from.HasValue && inv.IssueDate < from.Value) continue;
            if (to.HasValue && inv.IssueDate > to.Value) continue;
            running = Fees.Services.FeeCalculationService.Round2(running + inv.TotalAmount);
            lines.Add(new FeeStatementLineDto(inv.IssueDate, "Invoice", inv.InvoiceNumber, $"Invoice {inv.InvoiceNumber} {inv.Status}", inv.TotalAmount, 0m, running, inv.Currency));
        }
        foreach (var pay in payments)
        {
            if (from.HasValue && pay.PaymentDate < from.Value) continue;
            if (to.HasValue && pay.PaymentDate > to.Value) continue;
            running = Fees.Services.FeeCalculationService.Round2(running - pay.Amount);
            lines.Add(new FeeStatementLineDto(pay.PaymentDate, "Payment", pay.ReceiptNumber, $"Payment {pay.Method} {pay.Reference}", 0m, pay.Amount, running, pay.Currency));
        }

        var totalInvoiced = Fees.Services.FeeCalculationService.Round2(invoices.Sum(i => i.TotalAmount));
        var totalPaid = Fees.Services.FeeCalculationService.Round2(payments.Sum(p => p.Amount));
        var balance = Fees.Services.FeeCalculationService.Round2(totalInvoiced - totalPaid);
        var credit = Fees.Services.FeeCalculationService.Round2(credits.Where(c => !c.IsUtilized).Sum(c => c.Amount));

        return new FeeStatementDto(studentId, $"{student.FirstName} {student.LastName}", student.StudentNumber, lines.OrderBy(l => l.Date).ToList(), totalInvoiced, totalPaid, balance, credit, invoices.FirstOrDefault()?.Currency ?? "USD");
    }

    public async Task<List<InvoiceHistoryDto>> GetInvoiceHistoryAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);
        var invoices = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == studentId && !i.IsDeleted).OrderByDescending(i => i.IssueDate).ToListAsync(ct);
        return invoices.Select(i => new InvoiceHistoryDto(i.Id, i.InvoiceNumber, i.IssueDate, i.DueDate, i.TotalAmount, i.AmountPaid, i.BalanceDue, i.Status.ToString(), i.Currency)).ToList();
    }

    public async Task<List<ReceiptDto>> GetReceiptsAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);
        var payments = await _db.Set<Fees.Entities.Payment>().Where(p => p.TenantId == tenantId && p.StudentId == studentId && !p.IsDeleted).OrderByDescending(p => p.PaymentDate).ToListAsync(ct);
        return payments.Select(p => new ReceiptDto(p.Id, p.ReceiptNumber, p.PaymentDate, p.Amount, p.Currency, p.Method.ToString(), p.Reference)).ToList();
    }

    public async Task<List<AttendanceDetailDto>> GetAttendanceDetailAsync(long tenantId, long guardianId, long studentId, long? academicYearId, long? termId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);
        var query = _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == studentId && !a.IsDeleted);
        if (academicYearId.HasValue) query = query.Where(a => a.AcademicYearId == academicYearId.Value);
        if (termId.HasValue) query = query.Where(a => a.TermId == termId.Value);
        var records = await query.OrderByDescending(a => a.AttendanceDate).Take(100).ToListAsync(ct);
        var result = new List<AttendanceDetailDto>();
        foreach (var r in records)
        {
            var periodName = r.PeriodNumber.HasValue ? $"Period {r.PeriodNumber}" : "Daily";
            result.Add(new AttendanceDetailDto(r.AttendanceDate, r.Status.ToString(), r.AbsenceReason, r.Note, r.PeriodNumber, periodName));
        }
        return result;
    }

    public async Task<AttendanceSummaryDto> GetAttendanceSummaryAsync(long tenantId, long guardianId, long studentId, long academicYearId, long termId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);
        var records = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == studentId && a.AcademicYearId == academicYearId && a.TermId == termId && !a.IsDeleted).ToListAsync(ct);
        var total = records.Count;
        var present = records.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late || r.Status == AttendanceStatus.Excused);
        var absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        var late = records.Count(r => r.Status == AttendanceStatus.Late);
        var excused = records.Count(r => r.Status == AttendanceStatus.Excused);
        var sick = records.Count(r => r.Status == AttendanceStatus.Sick);
        var perc = total > 0 ? Math.Round((decimal)present / total * 100, 1) : 0m;
        return new AttendanceSummaryDto(total, present, absent, late, excused, sick, perc, perc < 85m && total > 10, "", "");
    }

    public async Task<List<ReportCardDto>> GetPublishedReportCardsAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);
        // Only published visible to parents
        var cards = await _db.Set<ReportCard>().Where(rc => rc.TenantId == tenantId && rc.StudentId == studentId && rc.Status == "published" && !rc.IsDeleted).OrderByDescending(rc => rc.PublishedAt).ToListAsync(ct);
        var result = new List<ReportCardDto>();
        foreach (var rc in cards)
        {
            var term = await _db.Set<Term>().FirstOrDefaultAsync(t => t.Id == rc.TermId, ct);
            result.Add(new ReportCardDto(rc.Id, term?.Name ?? $"Term {rc.TermId}", rc.AcademicYearId, rc.TermId, rc.TotalAverage, rc.OverallGradeLetter, rc.ClassRank, rc.ClassRank.HasValue ? $"Position {rc.ClassRank}" : null, rc.PublishedAt ?? rc.CreatedAt, rc.PdfUrl, rc.Status, rc.ClassTeacherComment, rc.HeadComment, rc.NextTermStartDate));
        }
        return result;
    }

    public async Task<ReportCardDetailDto> GetReportCardDetailAsync(long tenantId, long guardianId, long reportCardId, CancellationToken ct = default)
    {
        var rc = await _db.Set<ReportCard>().FirstOrDefaultAsync(r => r.Id == reportCardId && r.TenantId == tenantId && !r.IsDeleted, ct) ?? throw new InvalidOperationException("Report card not found");
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, rc.StudentId, ct);
        if (rc.Status != "published") throw new UnauthorizedAccessException("Report card not published, only published visible to parents");

        var subjects = await _db.Set<ReportCardSubject>().Where(s => s.TenantId == tenantId && s.ReportCardId == rc.Id && !s.IsDeleted).ToListAsync(ct);
        var subjectDtos = subjects.Select(s => new SubjectResultDto(s.SubjectId, s.SubjectName, s.Score, s.MaxScore, s.GradeLetter, s.TeacherComment, s.ClassAverage)).ToList();

        var attendance = await GetAttendanceSummaryAsync(tenantId, guardianId, rc.StudentId, rc.AcademicYearId, rc.TermId, ct);

        return new ReportCardDetailDto(
            new ReportCardDto(rc.Id, "", rc.AcademicYearId, rc.TermId, rc.TotalAverage, rc.OverallGradeLetter, rc.ClassRank, null, rc.PublishedAt ?? rc.CreatedAt, rc.PdfUrl, rc.Status, rc.ClassTeacherComment, rc.HeadComment, rc.NextTermStartDate),
            subjectDtos,
            attendance,
            new List<string>()
        );
    }

    public async Task<List<NoticeDto>> GetNoticesAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);
        // Simplified: messages where message_recipients contains guardian user id or broadcast to grade/stream
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null) return new List<NoticeDto>();

        var messages = await _db.Set<Messaging.Entities.MessageBatch>().Where(b => b.TenantId == tenantId && !b.IsDeleted).OrderByDescending(b => b.CreatedAt).Take(20).ToListAsync(ct);
        return messages.Select(m => new NoticeDto(m.Id, m.Title, m.Body, m.CreatedAt, "normal", false)).ToList();
    }

    public async Task<List<HomeworkDto>> GetHomeworkAsync(long tenantId, long guardianId, long studentId, CancellationToken ct = default)
    {
        await _authz.EnsureGuardianOfStudentAsync(tenantId, guardianId, studentId, ct);
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null) return new List<HomeworkDto>();

        var homework = await _db.Set<TeacherPortal.Entities.HomeworkAssignment>().Where(h => h.TenantId == tenantId && h.GradeId == student.GradeId && h.StreamId == student.StreamId && !h.IsDeleted && h.DueDate >= DateTime.UtcNow.Date.AddDays(-7)).OrderBy(h => h.DueDate).Take(20).ToListAsync(ct);
        var result = new List<HomeworkDto>();
        foreach (var hw in homework)
        {
            var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s => s.Id == hw.SubjectId, ct);
            result.Add(new HomeworkDto(hw.Id, hw.Title, subject?.Name ?? "", hw.DueDate, (hw.DueDate - DateTime.UtcNow.Date).Days, hw.Status));
        }
        return result;
    }

    public async Task<ParentProfileDto> GetProfileAsync(long tenantId, long guardianId, long userId, CancellationToken ct = default)
    {
        var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == guardianId && g.TenantId == tenantId && !g.IsDeleted, ct) ?? throw new InvalidOperationException("Guardian not found");
        var children = await GetChildrenAsync(tenantId, guardianId, ct);

        var pref = await _db.Set<GuardianContactPreference>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.GuardianId == guardianId && !p.IsDeleted, ct);
        var contactPref = pref != null
            ? new ContactPreferencesDto(!pref.SmsOptOut && pref.SmsOptIn, !pref.EmailOptOut && pref.EmailOptIn, pref.SmsOptOut, pref.EmailOptOut, pref.PreferredLanguage, true, true, true)
            : new ContactPreferencesDto(true, true, false, false, "en", true, true, true);

        return new ParentProfileDto(guardian.Id, userId, guardian.FirstName, guardian.LastName, $"{guardian.FirstName} {guardian.LastName}", guardian.Email, guardian.Phone, guardian.Address,
            children.Children,
            contactPref);
    }

    public async Task<ParentProfileDto> UpdateContactPreferencesAsync(long tenantId, long guardianId, long userId, UpdateContactPreferencesRequest req, CancellationToken ct = default)
    {
        var pref = await _db.Set<GuardianContactPreference>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.GuardianId == guardianId && !p.IsDeleted, ct);
        if (pref == null)
        {
            pref = new GuardianContactPreference { TenantId = tenantId, GuardianId = guardianId };
            _db.Set<GuardianContactPreference>().Add(pref);
        }

        pref.SmsOptIn = req.SmsOptIn && !req.SmsOptOut;
        pref.EmailOptIn = req.EmailOptIn && !req.EmailOptOut;
        pref.SmsOptOut = req.SmsOptOut;
        pref.EmailOptOut = req.EmailOptOut;
        pref.PreferredLanguage = req.PreferredLanguage;
        if (req.SmsOptOut) pref.SmsOptOutAt = DateTime.UtcNow;
        if (req.EmailOptOut) pref.EmailOptOutAt = DateTime.UtcNow;

        pref.UpdatedBy = userId;
        pref.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetProfileAsync(tenantId, guardianId, userId, ct);
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade/Stream/Guardian - single source of truth
