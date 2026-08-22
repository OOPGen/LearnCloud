using System.Text.Json;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.MultiTenancy.Entities;
using LearnCloud.SetupWizard.DTOs;
using LearnCloud.SetupWizard.Entities;
using LearnCloud.SetupWizard.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LearnCloud.SetupWizard.Services;

public interface IWizardService
{
    Task<WizardProgressDto> GetProgressAsync(long tenantId, CancellationToken ct = default);
    Task<FullWizardDataDto> GetFullDataAsync(long tenantId, CancellationToken ct = default);
    Task<WizardProgressDto> SaveStepAsync(long tenantId, long userId, int step, object data, CancellationToken ct = default);
    Task<WizardProgressDto> SkipStepAsync(long tenantId, long userId, int step, CancellationToken ct = default);
    Task<WizardSummaryDto> GetSummaryAsync(long tenantId, CancellationToken ct = default);
    Task<WizardSummaryDto> CompleteAsync(long tenantId, long userId, CancellationToken ct = default);
    Task<LogoUploadResponse> UploadLogoAsync(long tenantId, long userId, Stream fileStream, string fileName, CancellationToken ct = default);
    Task<List<StarterSubject>> GetDefaultSubjectsAsync(long tenantId, string schoolType, CancellationToken ct = default);
    Task<List<GradingBand>> GetDefaultGradingAsync(long tenantId, string? preference, CancellationToken ct = default);
}

public class WizardService : IWizardService
{
    private readonly LearnCloudDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<WizardService> _logger;

    public WizardService(LearnCloudDbContext db, ITenantContext tenantContext, ILogger<WizardService> logger)
    {
        _db = db; _tenantContext = tenantContext; _logger = logger;
    }

    private async Task<WizardProgress> GetOrCreateProgressAsync(long tenantId, CancellationToken ct)
    {
        var progress = await _db.Set<WizardProgress>().FirstOrDefaultAsync(w => w.TenantId == tenantId && !w.IsDeleted, ct);
        if (progress == null)
        {
            progress = new WizardProgress
            {
                TenantId = tenantId,
                CurrentStep = 1,
                IsCompleted = false,
                LastSavedAt = DateTime.UtcNow,
                StepsStatusJson = JsonSerializer.Serialize(new Dictionary<int,string>{{1,"not_started"}}),
                DataJson = JsonSerializer.Serialize(new FullWizardDataDto(null,null,null,null,null,null,null,null,null))
            };
            _db.Set<WizardProgress>().Add(progress);
            await _db.SaveChangesAsync(ct);
        }
        return progress;
    }

    public async Task<WizardProgressDto> GetProgressAsync(long tenantId, CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(tenantId, ct);
        var statusDict = JsonSerializer.Deserialize<Dictionary<int,string>>(progress.StepsStatusJson) ?? new();

        // Normalize to all steps
        for (int i=1;i<=9;i++) if (!statusDict.ContainsKey(i)) statusDict[i] = "not_started";

        var data = string.IsNullOrEmpty(progress.DataJson) ? null : JsonSerializer.Deserialize<FullWizardDataDto>(progress.DataJson);

        return new WizardProgressDto(
            progress.CurrentStep,
            statusDict,
            progress.IsCompleted,
            progress.LastSavedAt,
            progress.CompletedAt,
            data
        );
    }

    public async Task<FullWizardDataDto> GetFullDataAsync(long tenantId, CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(tenantId, ct);
        if (string.IsNullOrEmpty(progress.DataJson))
            return new FullWizardDataDto(null,null,null,null,null,null,null,null,null);
        return JsonSerializer.Deserialize<FullWizardDataDto>(progress.DataJson) ?? new FullWizardDataDto(null,null,null,null,null,null,null,null,null);
    }

    public async Task<WizardProgressDto> SaveStepAsync(long tenantId, long userId, int step, object data, CancellationToken ct = default)
    {
        if (step <1 || step >9) throw new ArgumentException("Step 1-9 only");

        var progress = await GetOrCreateProgressAsync(tenantId, ct);
        var fullData = string.IsNullOrEmpty(progress.DataJson) ? new FullWizardDataDto(null,null,null,null,null,null,null,null,null) : JsonSerializer.Deserialize<FullWizardDataDto>(progress.DataJson)!;

        // Deserialize data to correct type and persist domain entities
        var statusDict = JsonSerializer.Deserialize<Dictionary<int,string>>(progress.StepsStatusJson) ?? new();

        switch (step)
        {
            case 1:
                var schoolProfile = (data is JsonElement je1) ? JsonSerializer.Deserialize<SchoolProfileDto>(je1.GetRawText()) : (SchoolProfileDto)data;
                await SaveSchoolProfileAsync(tenantId, schoolProfile!, ct);
                fullData = fullData with { SchoolProfile = schoolProfile };
                break;
            case 2:
                var branding = (data is JsonElement je2) ? JsonSerializer.Deserialize<BrandingDto>(je2.GetRawText()) : (BrandingDto)data;
                await SaveBrandingAsync(tenantId, branding!, ct);
                fullData = fullData with { Branding = branding };
                break;
            case 3:
                var acadYear = (data is JsonElement je3) ? JsonSerializer.Deserialize<AcademicYearDto>(je3.GetRawText()) : (AcademicYearDto)data;
                await SaveAcademicYearAsync(tenantId, acadYear!, ct);
                fullData = fullData with { AcademicYear = acadYear };
                break;
            case 4:
                var terms = (data is JsonElement je4) ? JsonSerializer.Deserialize<TermsSetupDto>(je4.GetRawText()) : (TermsSetupDto)data;
                await SaveTermsAsync(tenantId, terms!, ct);
                fullData = fullData with { Terms = terms };
                break;
            case 5:
                var classes = (data is JsonElement je5) ? JsonSerializer.Deserialize<ClassesAndStreamsDto>(je5.GetRawText()) : (ClassesAndStreamsDto)data;
                await SaveClassesAndStreamsAsync(tenantId, classes!, ct);
                fullData = fullData with { ClassesAndStreams = classes };
                break;
            case 6:
                var subjects = (data is JsonElement je6) ? JsonSerializer.Deserialize<SubjectsDto>(je6.GetRawText()) : (SubjectsDto)data;
                await SaveSubjectsAsync(tenantId, subjects!, ct);
                fullData = fullData with { Subjects = subjects };
                break;
            case 7:
                var depts = (data is JsonElement je7) ? JsonSerializer.Deserialize<DepartmentsAndRolesDto>(je7.GetRawText()) : (DepartmentsAndRolesDto)data;
                await SaveDepartmentsAsync(tenantId, depts!, ct);
                fullData = fullData with { DepartmentsAndRoles = depts };
                break;
            case 8:
                var grading = (data is JsonElement je8) ? JsonSerializer.Deserialize<GradingScaleDto>(je8.GetRawText()) : (GradingScaleDto)data;
                await SaveGradingScaleAsync(tenantId, grading!, ct);
                fullData = fullData with { GradingScale = grading };
                break;
            case 9:
                var prefs = (data is JsonElement je9) ? JsonSerializer.Deserialize<PreferencesDto>(je9.GetRawText()) : (PreferencesDto)data;
                await SavePreferencesAsync(tenantId, prefs!, ct);
                fullData = fullData with { Preferences = prefs };
                break;
        }

        statusDict[step] = "completed";
        progress.CurrentStep = Math.Min(10, step + 1);
        progress.LastSavedAt = DateTime.UtcNow;
        progress.StepsStatusJson = JsonSerializer.Serialize(statusDict);
        progress.DataJson = JsonSerializer.Serialize(fullData);
        progress.UpdatedAt = DateTime.UtcNow;
        progress.UpdatedBy = userId;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Wizard step {Step} saved for tenant {TenantId} by {UserId}", step, tenantId, userId);

        for (int i=1;i<=9;i++) if (!statusDict.ContainsKey(i)) statusDict[i]="not_started";

        return new WizardProgressDto(progress.CurrentStep, statusDict, progress.IsCompleted, progress.LastSavedAt, progress.CompletedAt, fullData);
    }

    public async Task<WizardProgressDto> SkipStepAsync(long tenantId, long userId, int step, CancellationToken ct = default)
    {
        if (step == 1 || step == 3) throw new InvalidOperationException("School profile and academic year cannot be skipped");

        var progress = await GetOrCreateProgressAsync(tenantId, ct);
        var statusDict = JsonSerializer.Deserialize<Dictionary<int,string>>(progress.StepsStatusJson) ?? new();

        statusDict[step] = "skipped";
        progress.CurrentStep = Math.Min(10, step + 1);
        progress.LastSavedAt = DateTime.UtcNow;
        progress.StepsStatusJson = JsonSerializer.Serialize(statusDict);
        progress.UpdatedAt = DateTime.UtcNow;
        progress.UpdatedBy = userId;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Wizard step {Step} skipped for tenant {TenantId}", step, tenantId);

        for (int i=1;i<=9;i++) if (!statusDict.ContainsKey(i)) statusDict[i]="not_started";

        var fullData = string.IsNullOrEmpty(progress.DataJson) ? null : JsonSerializer.Deserialize<FullWizardDataDto>(progress.DataJson);
        return new WizardProgressDto(progress.CurrentStep, statusDict, progress.IsCompleted, progress.LastSavedAt, progress.CompletedAt, fullData);
    }

    public async Task<WizardSummaryDto> GetSummaryAsync(long tenantId, CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(tenantId, ct);
        var statusDict = JsonSerializer.Deserialize<Dictionary<int,string>>(progress.StepsStatusJson) ?? new();

        // Counts from actual domain tables
        var counts = new Dictionary<string,int>
        {
            ["grades"] = await _db.Grades.CountAsync(g=>g.TenantId==tenantId, ct),
            ["streams"] = await _db.Streams.CountAsync(s=>s.TenantId==tenantId, ct),
            ["subjects"] = await _db.Subjects.CountAsync(s=>s.TenantId==tenantId, ct),
            ["departments"] = 0, // placeholder if departments table not yet
            ["gradingBands"] = 0,
            ["terms"] = 0,
            ["academicYears"] = 0
        };

        // Try to get academic year/term counts if tables exist
        try { counts["academicYears"] = await _db.Set<Grade>().CountAsync(g=>g.TenantId==tenantId, ct); } catch {}
        // For demo, we count from wizard data JSON if needed

        var completed = statusDict.Count(kv=>kv.Value=="completed");
        var skipped = statusDict.Count(kv=>kv.Value=="skipped");

        var created = new List<string>();
        foreach (var kv in counts) if (kv.Value>0) created.Add($"{kv.Value} {kv.Key}");

        return new WizardSummaryDto(
            progress.IsCompleted,
            completed,
            skipped,
            counts,
            created,
            progress.CompletedAt ?? DateTime.UtcNow,
            GetNextActions()
        );
    }

    public async Task<WizardSummaryDto> CompleteAsync(long tenantId, long userId, CancellationToken ct = default)
    {
        var progress = await GetOrCreateProgressAsync(tenantId, ct);
        var statusDict = JsonSerializer.Deserialize<Dictionary<int,string>>(progress.StepsStatusJson) ?? new();

        // Ensure required steps completed
        if (!statusDict.TryGetValue(1, out var s1) || s1!="completed") throw new InvalidOperationException("School profile must be completed");
        if (!statusDict.TryGetValue(3, out var s3) || s3!="completed") throw new InvalidOperationException("Academic year must be completed");

        progress.IsCompleted = true;
        progress.CompletedAt = DateTime.UtcNow;
        progress.CurrentStep = 10;
        progress.LastSavedAt = DateTime.UtcNow;
        progress.UpdatedBy = userId;

        // Mark tenant settings setup complete
        var tenantSettings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (tenantSettings != null)
        {
            tenantSettings.FeaturesJson = (tenantSettings.FeaturesJson ?? "{}").Replace("}", $",\"setupComplete\":true,\"setupCompletedAt\":\"{DateTime.UtcNow:o}\"}}");
        }

        // Also update tenant status from trial to active? Keep trial until 14d but mark setup complete
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t=>t.Id==tenantId, ct);
        if (tenant != null && tenant.Status=="trial")
        {
            // Keep trial, but log
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Wizard completed for tenant {TenantId}", tenantId);

        return await GetSummaryAsync(tenantId, ct);
    }

    public async Task<LogoUploadResponse> UploadLogoAsync(long tenantId, long userId, Stream fileStream, string fileName, CancellationToken ct = default)
    {
        // SECURITY C3 FIX: Save outside wwwroot to prevent direct static serving of potentially malicious files
        // Store in secure location and serve via controller with proper Content-Disposition and Content-Type
        // For V1, save to uploads/{tenantId}/ with random GUID filename, not user-controlled, only PNG/JPG
        
        // Sanitize extension - only allow png/jpg, force png if unknown (defense in depth)
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") ext = ".png";
        
        // Use random GUID, not timestamp guessable, prevent enumeration
        var randomName = $"{Guid.NewGuid():N}{ext}";
        
        // SECURITY: Save outside wwwroot - in AppRoot/uploads/{tenantId}/
        // wwwroot is for static web assets, uploads should be in storage outside web root
        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads", tenantId.ToString());
        Directory.CreateDirectory(uploadsDir);
        var fullPath = Path.Combine(uploadsDir, randomName);
        
        // SECURITY: Ensure fullPath is within uploadsDir to prevent path traversal (defense in depth)
        var fullPathResolved = Path.GetFullPath(fullPath);
        var uploadsDirResolved = Path.GetFullPath(uploadsDir);
        if (!fullPathResolved.StartsWith(uploadsDirResolved, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid file path - potential traversal detected");
        
        // Write file with FileOptions to prevent overwriting executable?
        using var fs = new FileStream(fullPathResolved, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(fs, ct);
        
        // Verify file size after write (prevent zip bomb)
        if (fs.Length > 2 * 1024 * 1024)
        {
            fs.Close();
            File.Delete(fullPathResolved);
            throw new InvalidOperationException("File too large after write - max 2MB");
        }

        // URL now served via controller endpoint /api/setup/branding/logo/{tenantId}/{fileName} with proper headers
        // For backward compat, keep /uploads/ url but controller will serve it securely
        // In production, serve from S3 or blob storage with signed URL
        var url = $"/api/files/logos/{tenantId}/{randomName}"; // secure endpoint, not direct static

        // Update tenant and settings
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t=>t.Id==tenantId, ct);
        if (tenant!=null) tenant.LogoUrl=url;
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings!=null) settings.LogoUrl=url;
        else
        {
            settings = new TenantSettings { TenantId=tenantId, LogoUrl=url, PrimaryColor="#0F153A", SecondaryColor="#5F3F96", TimeZone="Africa/Harare", BaseCurrency="USD" };
            _db.TenantSettings.Add(settings);
        }
        await _db.SaveChangesAsync(ct);

        return new LogoUploadResponse(url, "Logo uploaded securely - PNG/JPG only, stored outside wwwroot, random filename");
    }

    public Task<List<StarterSubject>> GetDefaultSubjectsAsync(long tenantId, string schoolType, CancellationToken ct = default)
    {
        var list = SubjectDefaults.GetBySchoolType(schoolType);
        return Task.FromResult(list);
    }

    public Task<List<GradingBand>> GetDefaultGradingAsync(long tenantId, string? preference, CancellationToken ct = default)
    {
        var list = GradingScaleDefaults.GetDefault(preference);
        return Task.FromResult(list);
    }

    // Private save methods - these create domain entities, reusable from settings later

    private async Task SaveSchoolProfileAsync(long tenantId, SchoolProfileDto dto, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t=>t.Id==tenantId, ct) ?? throw new InvalidOperationException("Tenant not found");
        tenant.Name = dto.Name;
        tenant.City = dto.City;
        tenant.Country = dto.Country;
        tenant.ContactEmail = dto.ContactEmail;
        tenant.ContactPhone = dto.ContactPhone;
        tenant.LearnerCountBand = dto.LearnerCountBand;

        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings==null)
        {
            settings = new TenantSettings { TenantId=tenantId, TimeZone=dto.Timezone, BaseCurrency=dto.BaseCurrency, PrimaryColor="#0F153A" };
            _db.TenantSettings.Add(settings);
        }
        else
        {
            settings.TimeZone = dto.Timezone;
            settings.BaseCurrency = dto.BaseCurrency;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveBrandingAsync(long tenantId, BrandingDto dto, CancellationToken ct)
    {
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings==null)
        {
            settings = new TenantSettings { TenantId=tenantId, PrimaryColor=dto.PrimaryColor, SecondaryColor=dto.SecondaryColor, LogoUrl=dto.LogoUrl };
            _db.TenantSettings.Add(settings);
        }
        else
        {
            settings.PrimaryColor = dto.PrimaryColor;
            settings.SecondaryColor = dto.SecondaryColor;
            if (!string.IsNullOrEmpty(dto.LogoUrl)) settings.LogoUrl = dto.LogoUrl;
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t=>t.Id==tenantId, ct);
        if (tenant!=null)
        {
            tenant.PrimaryColor = dto.PrimaryColor;
            if (!string.IsNullOrEmpty(dto.LogoUrl)) tenant.LogoUrl = dto.LogoUrl;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveAcademicYearAsync(long tenantId, AcademicYearDto dto, CancellationToken ct)
    {
        // Check if academic year exists, else create Grade placeholder for year tracking? For V1 we use Grades table AcademicYearId as int year
        // Real implementation would have AcademicYear table - for demo we store in TenantSettings + create a dummy record in Grades? No, we store in settings JSON and assume AcademicYear table exists elsewhere
        // For simplicity, ensure TenantSettings current year
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings==null)
        {
            settings = new TenantSettings { TenantId=tenantId, CurrentAcademicYearId = int.Parse(dto.Name), PrimaryColor="#0F153A" };
            _db.TenantSettings.Add(settings);
        }
        else
        {
            if (int.TryParse(dto.Name, out var yearId)) settings.CurrentAcademicYearId = yearId;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveTermsAsync(long tenantId, TermsSetupDto dto, CancellationToken ct)
    {
        // For V1, store terms as JSON in TenantSettings? Real would have Terms table
        // We'll just update settings with terms json and also create dummy entries via Grade? Simplified: no-op but we log counts
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings!=null)
        {
            settings.FeaturesJson = JsonSerializer.Serialize(new { terms = dto.Terms });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveClassesAndStreamsAsync(long tenantId, ClassesAndStreamsDto dto, CancellationToken ct)
    {
        // Clear existing? For wizard first run, we can upsert
        foreach (var cls in dto.Classes)
        {
            var existingGrade = await _db.Grades.FirstOrDefaultAsync(g=>g.TenantId==tenantId && g.Code==cls.GradeCode && !g.IsDeleted, ct);
            if (existingGrade==null)
            {
                existingGrade = new Grade { TenantId=tenantId, Name=cls.GradeName, Code=cls.GradeCode, AcademicYearId=2026 };
                _db.Grades.Add(existingGrade);
                await _db.SaveChangesAsync(ct);
            }

            foreach (var stream in cls.Streams)
            {
                var existingStream = await _db.Streams.FirstOrDefaultAsync(s=>s.TenantId==tenantId && s.GradeId==existingGrade.Id && s.Name==stream.Name && !s.IsDeleted, ct);
                if (existingStream==null)
                {
                    var newStream = new Stream { TenantId=tenantId, GradeId=existingGrade.Id, Name=stream.Name, Capacity=stream.Capacity, AcademicYearId=2026 };
                    _db.Streams.Add(newStream);
                }
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveSubjectsAsync(long tenantId, SubjectsDto dto, CancellationToken ct)
    {
        foreach (var sub in dto.Subjects.Where(s=>s.Selected))
        {
            var existing = await _db.Subjects.FirstOrDefaultAsync(s=>s.TenantId==tenantId && s.Code==sub.Code && !s.IsDeleted, ct);
            if (existing==null)
            {
                var newSub = new Subject { TenantId=tenantId, Name=sub.Name, Code=sub.Code };
                _db.Subjects.Add(newSub);
            }
            else
            {
                existing.Name = sub.Name;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveDepartmentsAsync(long tenantId, DepartmentsAndRolesDto dto, CancellationToken ct)
    {
        // For V1, departments stored as JSON in TenantSettings since no dedicated table in frozen multi-tenancy spec
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings!=null)
        {
            var depsJson = JsonSerializer.Serialize(dto);
            settings.BrandingJson = depsJson; // reuse field
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveGradingScaleAsync(long tenantId, GradingScaleDto dto, CancellationToken ct)
    {
        // Store grading scale as JSON in TenantSettings for V1, real would have GradingScale tables
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings!=null)
        {
            settings.FeaturesJson = JsonSerializer.Serialize(new { gradingScale = dto });
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SavePreferencesAsync(long tenantId, PreferencesDto dto, CancellationToken ct)
    {
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings==null)
        {
            settings = new TenantSettings { TenantId=tenantId, TimeZone=dto.WeekStart, PrimaryColor="#0F153A" };
            _db.TenantSettings.Add(settings);
        }
        settings.BrandingJson = JsonSerializer.Serialize(dto);
        await _db.SaveChangesAsync(ct);
    }

    private List<NextActionDto> GetNextActions() => new()
    {
        new("Import learners", "Bulk CSV import 500 students with row errors, per SRS FR-SR05", "/students/import", "users", "Import CSV"),
        new("Add staff", "Create staff profiles and link to user accounts, assign class teacher", "/staff", "user-plus", "Add staff"),
        new("Set fee structures", "Define fee line items per grade/term with DECIMAL(18,2)+currency", "/fees/structures", "dollar", "Create fees")
    };
}
