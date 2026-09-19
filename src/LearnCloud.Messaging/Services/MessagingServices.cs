using LearnCloud.Fees.Entities;
using LearnCloud.AttendanceTimetable.Entities;
using System.Text.RegularExpressions;
using LearnCloud.Messaging.DTOs;
using LearnCloud.Messaging.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Messaging.Services;

public interface ITemplateService
{
    Task<List<TemplateDto>> ListAsync(long tenantId, CancellationToken ct = default);
    Task<TemplateDto> CreateAsync(long tenantId, long userId, CreateTemplateRequest req, CancellationToken ct = default);
    Task<TemplateDto> GetAsync(long tenantId, long templateId, CancellationToken ct = default);
    Task<string> RenderAsync(string templateBody, Dictionary<string,string> mergeData);
    Task<PreviewResponse> PreviewAsync(long tenantId, PreviewRequest req, CancellationToken ct = default);
}

public class TemplateService : ITemplateService
{
    private readonly LearnCloudDbContext _db;
    private static readonly Regex MergeFieldRegex = new(@"{{\s*(\w+)\s*}}", RegexOptions.Compiled);

    // Merge fields: learner name, class, amount owed, date, school name etc.
    public static readonly List<string> AvailableFields = new()
    {
        "learner_name", "learner_first_name", "learner_last_name", "learner_number", "class", "grade", "stream", "school_name", "amount_owed", "currency", "date", "due_date", "guardian_name", "guardian_first_name", "teacher_name", "next_term_date"
    };

    public TemplateService(LearnCloudDbContext db) => _db = db;

    public async Task<List<TemplateDto>> ListAsync(long tenantId, CancellationToken ct = default)
    {
        var templates = await _db.Set<MessageTemplate>().Where(t => t.TenantId == tenantId && !t.IsDeleted).ToListAsync(ct);
        return templates.Select(Map).ToList();
    }

    public async Task<TemplateDto> CreateAsync(long tenantId, long userId, CreateTemplateRequest req, CancellationToken ct = default)
    {
        var entity = new MessageTemplate
        {
            TenantId = tenantId,
            Name = req.Name,
            Code = req.Code,
            Channel = req.Channel.ToLower() == "sms" ? MessageChannel.Sms : MessageChannel.Email,
            Subject = req.Subject,
            Body = req.Body,
            Description = req.Description,
            IsSystem = req.IsSystem,
            CreatedBy = userId,
            MergeFieldsJson = System.Text.Json.JsonSerializer.Serialize(ExtractMergeFields(req.Body + " " + req.Subject))
        };
        _db.Set<MessageTemplate>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<TemplateDto> GetAsync(long tenantId, long templateId, CancellationToken ct = default)
    {
        var t = await _db.Set<MessageTemplate>().FirstOrDefaultAsync(x => x.Id == templateId && x.TenantId == tenantId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Template not found");
        return Map(t);
    }

    public Task<string> RenderAsync(string templateBody, Dictionary<string,string> mergeData)
    {
        var result = MergeFieldRegex.Replace(templateBody, match =>
        {
            var key = match.Groups[1].Value.ToLower();
            return mergeData.TryGetValue(key, out var val) ? val : match.Value; // keep placeholder if not found
        });
        return Task.FromResult(result);
    }

    public async Task<PreviewResponse> PreviewAsync(long tenantId, PreviewRequest req, CancellationToken ct = default)
    {
        // Resolve audience to get total recipients and sample
        var audienceResolver = new AudienceResolver(_db);
        // The full audience request (class, stream, amount owed...). This used to be reduced to
        // its type and passed to an overload that threw NotImplementedException, so every
        // message preview failed.
        var recipients = await audienceResolver.ResolveAsync(tenantId, req.Audience, ct);

        // Filter opt-out and no contact
        var filtered = await FilterOptOutAndNoContact(tenantId, recipients, req.Channel, ct);

        var total = recipients.Count;
        var filteredOutOptOut = filtered.OptedOutCount;
        var filteredOutNoContact = filtered.NoContactCount;
        var finalRecipients = filtered.FinalRecipients;

        // Pick sample recipient
        var sample = finalRecipients.FirstOrDefault();
        if (req.SampleGuardianId.HasValue)
        {
            sample = finalRecipients.FirstOrDefault(r => r.GuardianId == req.SampleGuardianId.Value) ?? sample;
        }

        string sampleName = sample?.GuardianName ?? "John Doe";
        string sampleAddress = sample?.Contact ?? "+263771234567";
        var mergeData = sample != null ? BuildMergeData(sample) : new Dictionary<string,string>
        {
            ["learner_name"] = "Thabo Ndlovu",
            ["class"] = "Grade 5 Blue",
            ["amount_owed"] = "450.00",
            ["currency"] = "USD",
            ["date"] = DateTime.UtcNow.ToString("dd/MM/yyyy"),
            ["school_name"] = "Petra High",
            ["guardian_name"] = sampleName
        };

        var renderedBody = await RenderAsync(req.Body, mergeData);
        var renderedSubject = await RenderAsync(req.Subject ?? "", mergeData);

        // Cost estimate
        var costEstimator = new CostEstimator(_db);
        var costEstimate = await costEstimator.EstimateAsync(tenantId, finalRecipients, req.Channel, ct);

        return new PreviewResponse(
            total,
            filteredOutOptOut,
            filteredOutNoContact,
            sampleName,
            sampleAddress,
            renderedSubject,
            renderedBody,
            ExtractMergeFields(req.Body).ToList(),
            MapCost(costEstimate)
        );
    }

    private TemplateDto Map(MessageTemplate t)
    {
        var fields = !string.IsNullOrEmpty(t.MergeFieldsJson) ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(t.MergeFieldsJson) ?? new List<string>() : ExtractMergeFields(t.Body + " " + t.Subject).ToList();
        return new TemplateDto(t.Id, t.Name, t.Code, t.Channel.ToString().ToLower(), t.Subject, t.Body, t.IsSystem, t.Description, fields);
    }

    private static IEnumerable<string> ExtractMergeFields(string body)
    {
        return MergeFieldRegex.Matches(body).Select(m => m.Groups[1].Value.ToLower()).Distinct();
    }

    private static Dictionary<string,string> BuildMergeData(RecipientInfo recipient)
    {
        return new Dictionary<string,string>
        {
            ["learner_name"] = recipient.StudentName ?? "Learner",
            ["learner_first_name"] = recipient.StudentFirstName ?? "Learner",
            ["learner_last_name"] = recipient.StudentLastName ?? "",
            ["learner_number"] = recipient.StudentNumber ?? "",
            ["class"] = recipient.ClassName ?? "",
            ["grade"] = recipient.GradeName ?? "",
            ["stream"] = recipient.StreamName ?? "",
            ["school_name"] = recipient.SchoolName ?? "School",
            ["amount_owed"] = recipient.AmountOwed?.ToString("F2") ?? "0.00",
            ["currency"] = recipient.Currency ?? "USD",
            ["date"] = DateTime.UtcNow.ToString("dd/MM/yyyy"),
            ["due_date"] = recipient.DueDate?.ToString("dd/MM/yyyy") ?? "",
            ["guardian_name"] = recipient.GuardianName ?? "",
            ["guardian_first_name"] = recipient.GuardianFirstName ?? "",
            ["teacher_name"] = recipient.TeacherName ?? "",
            ["next_term_date"] = recipient.NextTermDate?.ToString("dd/MM/yyyy") ?? ""
        };
    }


    private async Task<(List<RecipientInfo> FinalRecipients, int OptedOutCount, int NoContactCount)> FilterOptOutAndNoContact(long tenantId, List<RecipientInfo> recipients, string channel, CancellationToken ct)
    {
        var final = new List<RecipientInfo>();
        int optedOut = 0, noContact = 0;

        foreach (var r in recipients)
        {
            var pref = await _db.Set<GuardianContactPreference>().FirstOrDefaultAsync(p => p.TenantId == tenantId && p.GuardianId == r.GuardianId && !p.IsDeleted, ct);
            // Guardian contact preferences and opt-out always honoured
            if (pref != null)
            {
                if (channel.ToLower() == "sms" && (pref.SmsOptOut || !pref.SmsOptIn)) { optedOut++; continue; }
                if (channel.ToLower() == "email" && (pref.EmailOptOut || !pref.EmailOptIn)) { optedOut++; continue; }
            }

            if (string.IsNullOrWhiteSpace(r.Contact))
            {
                noContact++;
                continue;
            }

            final.Add(r);
        }

        return (final, optedOut, noContact);
    }

    private static CostEstimateDto MapCost(CostEstimate est) => new(
        est.TotalRecipients,
        est.SmsRecipients,
        est.EmailRecipients,
        est.FilteredOutOptOut,
        est.FilteredOutNoContact,
        est.CostPerSms,
        est.CostPerEmail,
        est.TotalCost,
        est.Currency,
        est.CapWarning,
        est.CapWarningMessage,
        est.CapExceeded,
        est.RemainingAfterSend
    );
}

// Audience selection
public class RecipientInfo
{
    public long GuardianId { get; set; }
    public string GuardianName { get; set; } = "";
    public string GuardianFirstName { get; set; } = "";
    public string Contact { get; set; } = ""; // phone or email
    public long? StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentFirstName { get; set; }
    public string? StudentLastName { get; set; }
    public string? StudentNumber { get; set; }
    public string? ClassName { get; set; }
    public string? GradeName { get; set; }
    public string? StreamName { get; set; }
    public string? SchoolName { get; set; }
    public decimal? AmountOwed { get; set; }
    public string? Currency { get; set; }
    public DateTime? DueDate { get; set; }
    public string? TeacherName { get; set; }
    public DateTime? NextTermDate { get; set; }
}

public class AudienceResolver
{
    private readonly LearnCloudDbContext _db;
    public AudienceResolver(LearnCloudDbContext db) => _db = db;


    public async Task<List<RecipientInfo>> ResolveAsync(long tenantId, AudienceRequest request, CancellationToken ct = default)
    {
        var type = request.Type.ToLower() switch
        {
            "class" => AudienceType.Class,
            "stream" => AudienceType.Stream,
            "year_group" => AudienceType.YearGroup,
            "all_guardians" => AudienceType.AllGuardians,
            "arrears_over_x" => AudienceType.ArrearsOverX,
            "absent_today" => AudienceType.AbsentToday,
            "manual" => AudienceType.Manual,
            _ => AudienceType.DynamicList
        };

        return await ResolveInternalAsync(tenantId, type, request, ct);
    }

    private async Task<List<RecipientInfo>> ResolveInternalAsync(long tenantId, AudienceType type, AudienceRequest req, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var schoolName = tenant?.Name ?? "School";

        List<RecipientInfo> result = new();

        switch (type)
        {
            case AudienceType.Class:
            case AudienceType.Stream:
                {
                    if (!req.GradeId.HasValue || !req.StreamId.HasValue) throw new InvalidOperationException("GradeId and StreamId required for class/stream audience");
                    var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.Id == req.GradeId.Value, ct);
                    var stream = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.Id == req.StreamId.Value, ct);

                    var enrolments = await _db.Set<StudentEnrolment>().Where(e => e.TenantId == tenantId && e.GradeId == req.GradeId.Value && e.StreamId == req.StreamId.Value && e.IsCurrent && !e.IsDeleted).ToListAsync(ct);
                    foreach (var enrol in enrolments)
                    {
                        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == enrol.StudentId && s.TenantId == tenantId, ct);
                        if (student == null) continue;
                        var guardianLinks = await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.StudentId == student.Id && !l.IsDeleted).ToListAsync(ct);
                        foreach (var link in guardianLinks)
                        {
                            var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == link.GuardianId && g.TenantId == tenantId, ct);
                            if (guardian == null) continue;

                            // For class/stream, get primary or billing guardian? For V1 we send to all linked guardians primary
                            result.Add(new RecipientInfo
                            {
                                GuardianId = guardian.Id,
                                GuardianName = $"{guardian.FirstName} {guardian.LastName}",
                                GuardianFirstName = guardian.FirstName,
                                Contact = req.Type == "email" || req.Type.Contains("email") ? guardian.Email ?? guardian.Phone : guardian.Phone, // will be filtered by channel later
                                StudentId = student.Id,
                                StudentName = $"{student.FirstName} {student.LastName}",
                                StudentFirstName = student.FirstName,
                                StudentLastName = student.LastName,
                                StudentNumber = student.StudentNumber,
                                ClassName = $"{grade?.Name} {stream?.Name}",
                                GradeName = grade?.Name,
                                StreamName = stream?.Name,
                                SchoolName = schoolName
                            });
                        }
                    }
                    break;
                }
            case AudienceType.AllGuardians:
                {
                    var guardians = await _db.Set<Guardian>().Where(g => g.TenantId == tenantId && !g.IsDeleted).ToListAsync(ct);
                    foreach (var g in guardians)
                    {
                        result.Add(new RecipientInfo
                        {
                            GuardianId = g.Id,
                            GuardianName = $"{g.FirstName} {g.LastName}",
                            GuardianFirstName = g.FirstName,
                            Contact = g.Phone, // channel decided later
                            SchoolName = schoolName
                        });
                    }
                    break;
                }
            case AudienceType.ArrearsOverX:
                {
                    if (!req.ArrearsThreshold.HasValue) throw new InvalidOperationException("ArrearsThreshold required");
                    // Guardians of learners with arrears over X - join fee_invoices balance_due > X
                    var invoices = await _db.Set<FeeInvoice>().Where(i => i.TenantId == tenantId && i.BalanceDue > req.ArrearsThreshold.Value && !i.IsDeleted).ToListAsync(ct);
                    var studentIds = invoices.Select(i => i.StudentId).Distinct().ToList();
                    foreach (var studentId in studentIds)
                    {
                        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId, ct);
                        var totalOwed = invoices.Where(i => i.StudentId == studentId).Sum(i => i.BalanceDue);
                        var links = await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.StudentId == studentId && l.IsBillingContact && !l.IsDeleted).ToListAsync(ct);
                        // If no billing contact flagged, take primary
                        if (!links.Any())
                            links = await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.StudentId == studentId && l.IsPrimaryContact && !l.IsDeleted).ToListAsync(ct);

                        foreach (var link in links)
                        {
                            var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == link.GuardianId, ct);
                            if (guardian == null) continue;
                            result.Add(new RecipientInfo
                            {
                                GuardianId = guardian.Id,
                                GuardianName = $"{guardian.FirstName} {guardian.LastName}",
                                GuardianFirstName = guardian.FirstName,
                                Contact = guardian.Phone,
                                StudentId = studentId,
                                StudentName = student != null ? $"{student.FirstName} {student.LastName}" : "",
                                StudentFirstName = student?.FirstName,
                                StudentLastName = student?.LastName,
                                StudentNumber = student?.StudentNumber,
                                AmountOwed = totalOwed,
                                Currency = invoices.First(i => i.StudentId == studentId).Currency,
                                DueDate = invoices.First(i => i.StudentId == studentId).DueDate,
                                SchoolName = schoolName
                            });
                        }
                    }
                    break;
                }
            case AudienceType.AbsentToday:
                {
                    var date = req.Date?.Date ?? DateTime.UtcNow.Date;
                    var absentRecords = await _db.Set<AttendanceRecord>().Where(a => a.TenantId == tenantId && a.AttendanceDate == date && (a.Status == AttendanceStatus.Absent || a.Status == AttendanceStatus.Sick) && !a.IsDeleted).ToListAsync(ct);
                    foreach (var rec in absentRecords)
                    {
                        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == rec.StudentId, ct);
                        var links = await _db.Set<GuardianStudentLink>().Where(l => l.TenantId == tenantId && l.StudentId == rec.StudentId && !l.IsDeleted).ToListAsync(ct);
                        foreach (var link in links)
                        {
                            var guardian = await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == link.GuardianId, ct);
                            if (guardian == null) continue;
                            result.Add(new RecipientInfo
                            {
                                GuardianId = guardian.Id,
                                GuardianName = $"{guardian.FirstName} {guardian.LastName}",
                                GuardianFirstName = guardian.FirstName,
                                Contact = guardian.Phone,
                                StudentId = rec.StudentId,
                                StudentName = student != null ? $"{student.FirstName} {student.LastName}" : "",
                                StudentFirstName = student?.FirstName,
                                StudentLastName = student?.LastName,
                                ClassName = "", // could lookup grade/stream
                                SchoolName = schoolName
                            });
                        }
                    }
                    break;
                }
            case AudienceType.Manual:
                {
                    if (req.ManualGuardianIds == null || !req.ManualGuardianIds.Any()) throw new InvalidOperationException("ManualGuardianIds required");
                    var guardians = await _db.Set<Guardian>().Where(g => g.TenantId == tenantId && req.ManualGuardianIds.Contains(g.Id) && !g.IsDeleted).ToListAsync(ct);
                    foreach (var g in guardians)
                    {
                        result.Add(new RecipientInfo
                        {
                            GuardianId = g.Id,
                            GuardianName = $"{g.FirstName} {g.LastName}",
                            GuardianFirstName = g.FirstName,
                            Contact = g.Phone,
                            SchoolName = schoolName
                        });
                    }
                    break;
                }
        }

        // Deduplicate by guardian_id + student_id (if same guardian has multiple children, keep separate rows per child for merge fields learner_name)
        // But for cost, we might want dedup per guardian if message not per learner? For V1 we keep per child for personalization
        return result;
    }
}

public class CostEstimator
{
    private readonly LearnCloudDbContext _db;
    public CostEstimator(LearnCloudDbContext db) => _db = db;

    public async Task<CostEstimate> EstimateAsync(long tenantId, List<RecipientInfo> finalRecipients, string channel, CancellationToken ct)
    {
        var providerSettings = await _db.Set<MessagingProviderSettings>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Channel.ToString().ToLower() == channel.ToLower() && s.IsActive && !s.IsDeleted, ct);
        var costPerSms = providerSettings?.CostPerSms ?? 0.05m;
        var costPerEmail = providerSettings?.CostPerEmail ?? 0.01m;

        var smsRecipients = channel.ToLower() == "sms" ? finalRecipients.Count : 0;
        var emailRecipients = channel.ToLower() == "email" ? finalRecipients.Count : 0;

        var totalCost = channel.ToLower() == "sms" ? smsRecipients * costPerSms : emailRecipients * costPerEmail;
        totalCost = Math.Round(totalCost, 2, MidpointRounding.AwayFromZero);

        // Per-tenant usage and cap
        var now = DateTime.UtcNow;
        var usage = await _db.Set<TenantMessagingUsage>().FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Year == now.Year && u.Month == now.Month && !u.IsDeleted, ct);
        var smsLimit = usage?.SmsLimit ?? providerSettings?.DailyCap ?? 1000;
        var emailLimit = usage?.EmailLimit ?? 5000;
        var smsUsed = usage?.SmsCount ?? 0;
        var emailUsed = usage?.EmailCount ?? 0;

        bool capWarning = false;
        string? warningMessage = null;
        bool capExceeded = false;

        if (channel.ToLower() == "sms")
        {
            var remaining = smsLimit - smsUsed;
            var after = remaining - smsRecipients;
            if (after < 0)
            {
                capExceeded = true;
                warningMessage = $"Hard cap exceeded: limit {smsLimit}, used {smsUsed}, trying to send {smsRecipients}, would exceed by {-after}. Reduce audience or increase cap.";
            }
            else if (after < smsLimit * 0.1) // less than 10% remaining
            {
                capWarning = true;
                warningMessage = $"Warning: You are about to hit your SMS cap. Limit {smsLimit}, used {smsUsed}, this send {smsRecipients}, remaining after {after}. Consider topping up.";
            }
        }
        else
        {
            var remaining = emailLimit - emailUsed;
            var after = remaining - emailRecipients;
            if (after < 0)
            {
                capExceeded = true;
                warningMessage = $"Hard cap exceeded: email limit {emailLimit}, used {emailUsed}, trying {emailRecipients}";
            }
            else if (after < emailLimit * 0.1)
            {
                capWarning = true;
                warningMessage = $"Warning: Near email cap. Remaining after send {after}.";
            }
        }

        var remainingAfter = channel.ToLower() == "sms" ? (smsLimit - smsUsed - smsRecipients) : (emailLimit - emailUsed - emailRecipients);

        return new CostEstimate
        {
            TotalRecipients = finalRecipients.Count,
            SmsRecipients = smsRecipients,
            EmailRecipients = emailRecipients,
            FilteredOutOptOut = 0, // calculated earlier, but here we pass already filtered, so 0
            FilteredOutNoContact = 0,
            CostPerSms = costPerSms,
            CostPerEmail = costPerEmail,
            TotalCost = totalCost,
            Currency = "USD",
            CapWarning = capWarning,
            CapWarningMessage = warningMessage,
            CapExceeded = capExceeded,
            RemainingAfterSend = remainingAfter
        };
    }
}

// Stub entities for compilation that already exist in other modules

// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Final cleanup - single source of truth
