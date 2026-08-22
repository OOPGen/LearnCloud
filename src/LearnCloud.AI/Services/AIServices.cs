using LearnCloud.AI.DTOs;
using LearnCloud.AI.Entities;
using LearnCloud.AI.Services.Providers;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.AI.Services;

public interface IReportCommentService
{
    Task<GenerateCommentResponse> GenerateDraftAsync(long tenantId, long userId, GenerateCommentRequest req, CancellationToken ct = default);
    Task<ReportCommentDraft> ReviewAsync(long tenantId, long userId, ReviewCommentRequest req, CancellationToken ct = default);
    Task<ReportCommentDraft> SaveAsync(long tenantId, long userId, SaveCommentRequest req, CancellationToken ct = default);
    Task<List<ReportCommentDraft>> ListDraftsAsync(long tenantId, long studentId, CancellationToken ct = default);
}

public class ReportCommentService : IReportCommentService
{
    private readonly LearnCloudDbContext _db;
    private readonly IAIProviderFactory _providerFactory;

    public ReportCommentService(LearnCloudDbContext db, IAIProviderFactory providerFactory)
    {
        _db = db;
        _providerFactory = providerFactory;
    }

    public async Task<GenerateCommentResponse> GenerateDraftAsync(long tenantId, long userId, GenerateCommentRequest req, CancellationToken ct = default)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == req.StudentId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Student not found");
        var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == student.GradeId, ct);
        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.Id == student.StreamId, ct);

        // Get marks, attendance, subject performance
        var marks = await _db.Set<StudentMark>().Where(m => m.TenantId == tenantId && m.StudentId == req.StudentId && m.AcademicYearId == req.AcademicYearId && m.TermId == req.TermId && !m.IsDeleted).ToListAsync(ct);
        var subjects = await _db.Set<Subject>().Where(s => s.TenantId == tenantId && !s.IsDeleted).ToListAsync(ct);
        var attendanceRecords = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == req.StudentId && a.AcademicYearId == req.AcademicYearId && a.TermId == req.TermId && !a.IsDeleted).ToListAsync(ct);

        // Build subject performances
        var subjectPerformances = new List<SubjectPerformanceDto>();
        var groupedBySubject = marks.GroupBy(m => m.SubjectId);
        foreach (var g in groupedBySubject)
        {
            var subject = subjects.FirstOrDefault(s => s.Id == g.Key);
            var scores = g.Select(m => m.Score).Where(s => s.HasValue).Select(s => s.Value).ToList();
            var avg = scores.Any() ? scores.Average() : 0;
            var max = g.First().AssessmentId != 0 ? 100m : 100m; // simplified max
            // Trend: compare to previous term if exists
            var prevMarks = await _db.Set<StudentMark>().Where(m => m.TenantId == tenantId && m.StudentId == req.StudentId && m.SubjectId == g.Key && m.TermId != req.TermId && !m.IsDeleted).ToListAsync(ct);
            var prevAvg = prevMarks.Any(m => m.Score.HasValue) ? prevMarks.Where(m => m.Score.HasValue).Average(m => m.Score!.Value) : avg;
            var trend = avg > prevAvg + 5 ? "up" : avg < prevAvg - 5 ? "down" : "stable";
            var strength = avg >= 70 ? "strength" : avg < 50 ? "weakness" : "average";

            subjectPerformances.Add(new SubjectPerformanceDto(g.Key, subject?.Name ?? $"Subject {g.Key}", avg, 100m, null, null, trend, strength));
        }

        // Attendance for comment
        var totalDays = attendanceRecords.Count;
        var present = attendanceRecords.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late);
        var absent = attendanceRecords.Count(r => r.Status == AttendanceStatus.Absent);
        var late = attendanceRecords.Count(r => r.Status == AttendanceStatus.Late);
        var perc = totalDays > 0 ? Math.Round((decimal)present / totalDays * 100, 1) : 0m;

        var attendanceDto = new AttendanceForCommentDto(totalDays, present, absent, late, perc, $"{perc}% - {present} present, {absent} absent");

        // Aggregate and position
        var aggregate = subjectPerformances.Where(s => s.Score.HasValue).Sum(s => s.Score!.Value);
        var average = subjectPerformances.Any(s => s.Score.HasValue) ? subjectPerformances.Where(s => s.Score.HasValue).Average(s => s.Score!.Value) : 0m;

        // Build input for AI provider
        var input = new CommentGenerationInput
        {
            StudentId = req.StudentId,
            StudentName = $"{student.FirstName} {student.LastName}",
            GradeName = grade?.Name ?? "",
            StreamName = stream?.Name ?? "",
            SubjectPerformances = subjectPerformances,
            Attendance = attendanceDto,
            Aggregate = aggregate,
            Average = average,
            Position = null, // would get from merit list
            Tone = req.Tone,
            Length = req.Length,
            CustomInstructions = req.CustomInstructions
        };

        var provider = await _providerFactory.GetProviderAsync(tenantId, ct);
        var result = await provider.GenerateReportCommentAsync(input, ct);

        // Save draft - teacher always reviews and edits before saving; nothing written automatically to report card
        var draft = new ReportCommentDraft
        {
            TenantId = tenantId,
            StudentId = req.StudentId,
            AcademicYearId = req.AcademicYearId,
            TermId = req.TermId,
            ReportCardId = req.ReportCardId,
            InputDataJson = System.Text.Json.JsonSerializer.Serialize(input),
            DraftComment = result.DraftComment,
            Tone = req.Tone,
            Length = req.Length,
            ProviderName = result.ProviderName,
            Model = result.Model,
            PromptTokens = result.PromptTokens,
            CompletionTokens = result.CompletionTokens,
            Cost = result.Cost,
            CreatedBy = userId
        };
        _db.Set<ReportCommentDraft>().Add(draft);
        await _db.SaveChangesAsync(ct);

        return new GenerateCommentResponse(
            draft.Id,
            req.StudentId,
            $"{student.FirstName} {student.LastName}",
            result.DraftComment,
            req.Tone,
            req.Length,
            subjectPerformances,
            attendanceDto,
            result.ProviderName,
            result.Model,
            true // requires review
        );
    }

    public async Task<ReportCommentDraft> ReviewAsync(long tenantId, long userId, ReviewCommentRequest req, CancellationToken ct = default)
    {
        var draft = await _db.Set<ReportCommentDraft>().FirstOrDefaultAsync(d => d.Id == req.DraftId && d.TenantId == tenantId && !d.IsDeleted, ct) ?? throw new InvalidOperationException("Draft not found");
        draft.EditedComment = req.EditedComment;
        draft.IsEdited = true;
        draft.EditedByUserId = userId;
        draft.EditedAt = DateTime.UtcNow;
        draft.UpdatedBy = userId;
        await _db.SaveChangesAsync(ct);
        return draft;
    }

    public async Task<ReportCommentDraft> SaveAsync(long tenantId, long userId, SaveCommentRequest req, CancellationToken ct = default)
    {
        var draft = await _db.Set<ReportCommentDraft>().FirstOrDefaultAsync(d => d.Id == req.DraftId && d.TenantId == tenantId && !d.IsDeleted, ct) ?? throw new InvalidOperationException("Draft not found");
        if (!draft.IsEdited && draft.DraftComment != req.FinalComment)
        {
            // Allow save even if not explicitly reviewed? But requirement: teacher always reviews and edits before saving
            // For V1 we allow save but mark as edited
            draft.IsEdited = true;
            draft.EditedByUserId = userId;
            draft.EditedAt = DateTime.UtcNow;
        }

        draft.EditedComment = req.FinalComment;
        draft.IsSaved = true;
        draft.SavedByUserId = userId;
        draft.SavedAt = DateTime.UtcNow;

        // Here we would update the actual report card comment: e.g. ReportCard.ClassTeacherComment = req.FinalComment
        // But nothing written automatically - teacher must explicitly save as final step, which we do now after review
        // So we update report card only upon explicit save, not on generation
        if (draft.ReportCardId.HasValue)
        {
            var reportCard = await _db.Set<ReportCard>().FirstOrDefaultAsync(rc => rc.Id == draft.ReportCardId.Value && rc.TenantId == tenantId, ct);
            if (reportCard != null)
            {
                reportCard.ClassTeacherComment = req.FinalComment;
                // Audit log
                _db.AuditLogs.Add(new AuditLog
                {
                    TenantId = tenantId,
                    UserId = userId,
                    EntityType = "ReportCard",
                    EntityId = reportCard.Id,
                    Action = "update_comment_via_ai_draft",
                    OldValues = $"{{\"draftId\":{draft.Id}}}",
                    NewValues = $"{{\"finalComment\":\"{req.FinalComment.Substring(0, Math.Min(100, req.FinalComment.Length))}...\"}}",
                    CreatedBy = userId
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return draft;
    }

    public async Task<List<ReportCommentDraft>> ListDraftsAsync(long tenantId, long studentId, CancellationToken ct = default)
    {
        return await _db.Set<ReportCommentDraft>().Where(d => d.TenantId == tenantId && d.StudentId == studentId && !d.IsDeleted).OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
    }
}

// Attendance anomaly detection
public interface IAttendanceAnomalyService
{
    Task<List<AttendanceAnomaly>> DetectAsync(long tenantId, long? gradeId, long? streamId, long? academicYearId, long? termId, int daysBack, decimal threshold, CancellationToken ct = default);
    Task<List<AttendanceAnomalyDto>> ListAsync(long tenantId, long? studentId, CancellationToken ct = default);
}

public class AttendanceAnomalyService : IAttendanceAnomalyService
{
    private readonly LearnCloudDbContext _db;

    public AttendanceAnomalyService(LearnCloudDbContext db) => _db = db;

    public async Task<List<AttendanceAnomaly>> DetectAsync(long tenantId, long? gradeId, long? streamId, long? academicYearId, long? termId, int daysBack, decimal threshold, CancellationToken ct = default)
    {
        var fromDate = DateTime.UtcNow.AddDays(-daysBack);
        var query = _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.AttendanceDate >= fromDate && !a.IsDeleted);
        if (gradeId.HasValue) query = query.Where(a => a.GradeId == gradeId.Value);
        if (streamId.HasValue) query = query.Where(a => a.StreamId == streamId.Value);
        if (academicYearId.HasValue) query = query.Where(a => a.AcademicYearId == academicYearId.Value);
        if (termId.HasValue) query = query.Where(a => a.TermId == termId.Value);

        var records = await query.ToListAsync(ct);
        var anomalies = new List<AttendanceAnomaly>();

        var groupedByStudent = records.GroupBy(r => r.StudentId);
        foreach (var group in groupedByStudent)
        {
            var studentId = group.Key;
            var studentRecords = group.OrderBy(r => r.AttendanceDate).ToList();

            // Flag specific weekday consistently missed
            var weekdayGroups = studentRecords.GroupBy(r => r.AttendanceDate.DayOfWeek);
            foreach (var weekdayGroup in weekdayGroups)
            {
                var total = weekdayGroup.Count();
                var missed = weekdayGroup.Count(r => r.Status == AttendanceStatus.Absent || r.Status == AttendanceStatus.Sick);
                if (total >= 3 && (decimal)missed / total * 100 >= threshold)
                {
                    anomalies.Add(new AttendanceAnomaly
                    {
                        TenantId = tenantId,
                        StudentId = studentId,
                        AnomalyType = "weekday_pattern",
                        Description = $"Consistently missed {weekdayGroup.Key}s",
                        Explanation = $"Student missed {missed} out of {total} {weekdayGroup.Key}s in last {daysBack} days ({Math.Round((decimal)missed / total * 100, 1)}% miss rate on {weekdayGroup.Key}s). Possible transport issue or weekly commitment. Check with guardian.",
                        ConfidenceScore = Math.Round((decimal)missed / total * 100, 1),
                        PeriodFrom = fromDate,
                        PeriodTo = DateTime.UtcNow,
                        DataJson = $"{{\"weekday\":\"{weekdayGroup.Key}\",\"missed\":{missed},\"total\":{total},\"rate\":{Math.Round((decimal)missed / total * 100, 1)}}}",
                        Status = "new"
                    });
                }
            }

            // Sudden drop: compare last 7 days vs previous 21 days
            var last7 = studentRecords.Where(r => r.AttendanceDate >= DateTime.UtcNow.AddDays(-7)).ToList();
            var prev21 = studentRecords.Where(r => r.AttendanceDate >= DateTime.UtcNow.AddDays(-28) && r.AttendanceDate < DateTime.UtcNow.AddDays(-7)).ToList();

            if (last7.Any() && prev21.Any())
            {
                var last7Rate = (decimal)last7.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late) / last7.Count * 100;
                var prev21Rate = (decimal)prev21.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late) / prev21.Count * 100;
                var drop = prev21Rate - last7Rate;
                if (drop >= 20) // sudden drop >20%
                {
                    anomalies.Add(new AttendanceAnomaly
                    {
                        TenantId = tenantId,
                        StudentId = studentId,
                        AnomalyType = "sudden_drop",
                        Description = $"Sudden attendance drop {drop:F1}%",
                        Explanation = $"Attendance dropped from {prev21Rate:F1}% (previous 21 days, {prev21.Count} records) to {last7Rate:F1}% (last 7 days, {last7.Count} records), drop of {drop:F1}%. Possible illness, family issue, or disengagement. Pastoral follow-up recommended.",
                        ConfidenceScore = Math.Min(drop * 2, 95),
                        PeriodFrom = prev21.Min(r => r.AttendanceDate),
                        PeriodTo = DateTime.UtcNow,
                        DataJson = $"{{\"prev21Rate\":{prev21Rate:F1},\"last7Rate\":{last7Rate:F1},\"drop\":{drop:F1}}}",
                        Status = "new"
                    });
                }
            }

            // Consecutive absence N days
            var consecutive = 0;
            var maxConsecutive = 0;
            var consecutiveStart = DateTime.MinValue;
            var currentStart = DateTime.MinValue;

            foreach (var rec in studentRecords.OrderBy(r => r.AttendanceDate))
            {
                if (rec.Status == AttendanceStatus.Absent || rec.Status == AttendanceStatus.Sick)
                {
                    if (consecutive == 0) currentStart = rec.AttendanceDate;
                    consecutive++;
                    maxConsecutive = Math.Max(maxConsecutive, consecutive);
                }
                else
                {
                    if (consecutive >= 3)
                    {
                        anomalies.Add(new AttendanceAnomaly
                        {
                            TenantId = tenantId,
                            StudentId = studentId,
                            AnomalyType = "consecutive_absence",
                            Description = $"{consecutive} consecutive days absent",
                            Explanation = $"Student absent for {consecutive} consecutive days from {currentStart:dd/MM/yyyy} to {rec.AttendanceDate.AddDays(-1):dd/MM/yyyy}. Check welfare, contact guardian.",
                            ConfidenceScore = Math.Min(consecutive * 10, 95),
                            PeriodFrom = currentStart,
                            PeriodTo = rec.AttendanceDate,
                            DataJson = $"{{\"consecutive\":{consecutive}}}",
                            Status = "new"
                        });
                    }
                    consecutive = 0;
                }
            }

            if (consecutive >= 3)
            {
                anomalies.Add(new AttendanceAnomaly
                {
                    TenantId = tenantId,
                    StudentId = studentId,
                    AnomalyType = "consecutive_absence",
                    Description = $"{consecutive} consecutive days absent (ongoing)",
                    Explanation = $"Currently absent for {consecutive} consecutive days since {currentStart:dd/MM/yyyy}. Immediate follow-up needed.",
                    ConfidenceScore = Math.Min(consecutive * 10, 95),
                    PeriodFrom = currentStart,
                    PeriodTo = DateTime.UtcNow,
                    DataJson = $"{{\"consecutive\":{consecutive}}}",
                    Status = "new"
                });
            }
        }

        // Save detected anomalies, avoid duplicates
        foreach (var anomaly in anomalies)
        {
            var exists = await _db.Set<AttendanceAnomaly>().AnyAsync(a => a.TenantId == tenantId && a.StudentId == anomaly.StudentId && a.AnomalyType == anomaly.AnomalyType && a.Status == "new" && a.PeriodFrom == anomaly.PeriodFrom && !a.IsDeleted, ct);
            if (!exists)
            {
                _db.Set<AttendanceAnomaly>().Add(anomaly);
            }
        }
        await _db.SaveChangesAsync(ct);

        return anomalies;
    }

    public async Task<List<AttendanceAnomalyDto>> ListAsync(long tenantId, long? studentId, CancellationToken ct = default)
    {
        var query = _db.Set<AttendanceAnomaly>().Where(a => a.TenantId == tenantId && !a.IsDeleted);
        if (studentId.HasValue) query = query.Where(a => a.StudentId == studentId.Value);

        var list = await query.OrderByDescending(a => a.DetectedAt).Take(100).ToListAsync(ct);
        var result = new List<AttendanceAnomalyDto>();
        foreach (var a in list)
        {
            var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == a.StudentId, ct);
            result.Add(new AttendanceAnomalyDto(a.Id, a.StudentId, student != null ? $"{student.FirstName} {student.LastName}" : "", student?.StudentNumber ?? "", a.AnomalyType, a.Description, a.Explanation, a.ConfidenceScore, a.DetectedAt, a.PeriodFrom, a.PeriodTo, a.Status, a.DataJson));
        }
        return result;
    }
}

// At-risk learner identification
public interface IAtRiskService
{
    Task<List<AtRiskFlag>> DetectAsync(long tenantId, long? gradeId, long? streamId, long? academicYearId, long? termId, decimal marksDropThreshold, decimal attendanceDropThreshold, decimal arrearsThreshold, CancellationToken ct = default);
    Task<List<AtRiskFlagDto>> ListAsync(long tenantId, long? studentId, CancellationToken ct = default);
}

public class AtRiskService : IAtRiskService
{
    private readonly LearnCloudDbContext _db;

    public AtRiskService(LearnCloudDbContext db) => _db = db;

    public async Task<List<AtRiskFlag>> DetectAsync(long tenantId, long? gradeId, long? streamId, long? academicYearId, long? termId, decimal marksDropThreshold = 15m, decimal attendanceDropThreshold = 15m, decimal arrearsThreshold = 100m, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var fromDate = now.AddDays(-60); // look back 60 days

        var studentsQuery = _db.Set<Student>().Where(s => s.TenantId == tenantId && !s.IsDeleted);
        if (gradeId.HasValue) studentsQuery = studentsQuery.Where(s => s.GradeId == gradeId.Value);
        if (streamId.HasValue) studentsQuery = studentsQuery.Where(s => s.StreamId == streamId.Value);

        var students = await studentsQuery.ToListAsync(ct);
        var flags = new List<AtRiskFlag>();

        foreach (var student in students)
        {
            var reasons = new List<UnderlyingReasonDto>();
            decimal riskScore = 0m;

            // Falling marks: compare current term average vs previous term
            var currentMarks = await _db.Set<StudentMark>().Where(m => m.TenantId == tenantId && m.StudentId == student.Id && (academicYearId == null || m.AcademicYearId == academicYearId) && (termId == null || m.TermId == termId) && !m.IsDeleted && m.Score.HasValue).ToListAsync(ct);
            var prevMarks = await _db.Set<StudentMark>().Where(m => m.TenantId == tenantId && m.StudentId == student.Id && (termId == null || m.TermId != termId) && !m.IsDeleted && m.Score.HasValue).OrderByDescending(m => m.CreatedAt).Take(20).ToListAsync(ct);

            if (currentMarks.Any() && prevMarks.Any())
            {
                var currentAvg = currentMarks.Average(m => m.Score!.Value);
                var prevAvg = prevMarks.Average(m => m.Score!.Value);
                var drop = prevAvg - currentAvg;
                if (drop >= marksDropThreshold)
                {
                    reasons.Add(new UnderlyingReasonDto("falling_marks", $"Marks dropped {drop:F1}% from {prevAvg:F1}% to {currentAvg:F1}% (current {currentMarks.Count} marks vs previous {prevMarks.Count})", drop >= 20 ? "high" : "medium", drop, $"down"));
                    riskScore += drop >= 20 ? 40 : 25;
                }
            }

            // Declining attendance
            var attendanceRecords = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.StudentId == student.Id && !a.IsDeleted).OrderByDescending(a => a.AttendanceDate).Take(40).ToListAsync(ct);
            if (attendanceRecords.Count >= 10)
            {
                var last10 = attendanceRecords.Take(10).ToList();
                var prev10 = attendanceRecords.Skip(10).Take(10).ToList();
                if (last10.Any() && prev10.Any())
                {
                    var last10Rate = (decimal)last10.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late) / last10.Count * 100;
                    var prev10Rate = (decimal)prev10.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late) / prev10.Count * 100;
                    var attDrop = prev10Rate - last10Rate;
                    if (attDrop >= attendanceDropThreshold)
                    {
                        reasons.Add(new UnderlyingReasonDto("declining_attendance", $"Attendance dropped {attDrop:F1}% from {prev10Rate:F1}% to {last10Rate:F1}% (last 10 vs previous 10)", attDrop >= 20 ? "high" : "medium", attDrop, "down"));
                        riskScore += attDrop >= 20 ? 35 : 20;
                    }
                }

                // Overall low attendance
                var total = attendanceRecords.Count;
                var present = attendanceRecords.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late);
                var perc = total > 0 ? (decimal)present / total * 100 : 0m;
                if (perc < 85m)
                {
                    reasons.Add(new UnderlyingReasonDto("low_attendance", $"Overall attendance {perc:F1}% below 85% threshold ({present}/{total} present)", perc < 70 ? "high" : "medium", perc, "low"));
                    riskScore += perc < 70 ? 30 : 15;
                }
            }

            // Fee arrears
            var invoices = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.StudentId == student.Id && !i.IsDeleted && i.BalanceDue > 0).ToListAsync(ct);
            var totalArrears = invoices.Sum(i => i.BalanceDue);
            if (totalArrears >= arrearsThreshold)
            {
                reasons.Add(new UnderlyingReasonDto("fee_arrears", $"Arrears {totalArrears:F2} {invoices.FirstOrDefault()?.Currency ?? "USD"} over threshold {arrearsThreshold} - {invoices.Count} unpaid invoices", totalArrears >= arrearsThreshold * 2 ? "high" : "medium", totalArrears, "up"));
                riskScore += totalArrears >= arrearsThreshold * 2 ? 30 : 20;
            }

            // If any reason, create flag
            if (reasons.Any())
            {
                var riskLevel = riskScore >= 70 ? "critical" : riskScore >= 50 ? "high" : riskScore >= 30 ? "medium" : "low";
                var flagReason = $"{string.Join("; ", reasons.Select(r => r.Detail))}";

                // Avoid duplicate active flag
                var existing = await _db.Set<AtRiskFlag>().FirstOrDefaultAsync(f => f.TenantId == tenantId && f.StudentId == student.Id && f.Status == "new" && !f.IsDeleted, ct);
                if (existing == null)
                {
                    var flag = new AtRiskFlag
                    {
                        TenantId = tenantId,
                        StudentId = student.Id,
                        RiskLevel = riskLevel,
                        RiskScore = Math.Min(riskScore, 100),
                        FlagReason = flagReason,
                        UnderlyingReasonsJson = System.Text.Json.JsonSerializer.Serialize(reasons),
                        DetectedAt = now,
                        PeriodFrom = fromDate,
                        PeriodTo = now,
                        Status = "new"
                    };
                    _db.Set<AtRiskFlag>().Add(flag);
                    flags.Add(flag);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return flags;
    }

    public async Task<List<AtRiskFlagDto>> ListAsync(long tenantId, long? studentId, CancellationToken ct = default)
    {
        var query = _db.Set<AtRiskFlag>().Where(f => f.TenantId == tenantId && !f.IsDeleted);
        if (studentId.HasValue) query = query.Where(f => f.StudentId == studentId.Value);

        var list = await query.OrderByDescending(f => f.DetectedAt).Take(100).ToListAsync(ct);
        var result = new List<AtRiskFlagDto>();
        foreach (var f in list)
        {
            var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == f.StudentId, ct);
            var grade = student != null ? await _db.Grades.FirstOrDefaultAsync(g => g.Id == student.GradeId, ct) : null;
            var stream = student != null ? await _db.Streams.FirstOrDefaultAsync(s => s.Id == student.StreamId, ct) : null;
            var reasons = System.Text.Json.JsonSerializer.Deserialize<List<UnderlyingReasonDto>>(f.UnderlyingReasonsJson) ?? new List<UnderlyingReasonDto>();

            result.Add(new AtRiskFlagDto(
                f.Id,
                f.StudentId,
                student != null ? $"{student.FirstName} {student.LastName}" : "",
                student?.StudentNumber ?? "",
                grade?.Name ?? "",
                stream?.Name ?? "",
                f.RiskLevel,
                f.RiskScore,
                f.FlagReason,
                reasons,
                f.DetectedAt,
                f.Status,
                f.AssignedToUserId,
                null
            ));
        }
        return result;
    }
}

// Stub entities for compilation

// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Final cleanup - single source of truth
