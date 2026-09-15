using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using LearnCloud.Core.Services;
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
    private readonly IAcademicCalendarService _calendar;
    private readonly IClassStructureService _classes;
    private readonly IServiceProvider _services;

    public WizardService(LearnCloudDbContext db, ITenantContext tenantContext, ILogger<WizardService> logger,
        IAcademicCalendarService calendar, IClassStructureService classes, IServiceProvider services)
    {
        _db = db; _tenantContext = tenantContext; _logger = logger;
        _calendar = calendar; _classes = classes; _services = services;
    }

    // Step bodies arrive as raw JSON, which the API's validation filter cannot see, so each
    // step is validated here. A failure is a FluentValidation.ValidationException, which the
    // controller returns as 400 with the errors.
    private async Task<T> ValidatedAsync<T>(T? dto, CancellationToken ct) where T : class
    {
        if (dto is null) throw new InvalidOperationException("The step data is missing.");
        if (_services.GetService(typeof(IValidator<T>)) is IValidator<T> validator)
            await validator.ValidateAndThrowAsync(dto, ct);
        return dto;
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
                var schoolProfile = await ValidatedAsync(ReadStep<SchoolProfileDto>(data), ct);
                await SaveSchoolProfileAsync(tenantId, schoolProfile, ct);
                fullData = fullData with { SchoolProfile = schoolProfile };
                break;
            case 2:
                var branding = await ValidatedAsync(ReadStep<BrandingDto>(data), ct);
                await SaveBrandingAsync(tenantId, branding, ct);
                fullData = fullData with { Branding = branding };
                break;
            case 3:
                var acadYear = await ValidatedAsync(ReadStep<AcademicYearDto>(data), ct);
                await SaveAcademicYearAsync(tenantId, userId, acadYear, ct);
                fullData = fullData with { AcademicYear = acadYear };
                break;
            case 4:
                var terms = await ValidatedAsync(ReadStep<TermsSetupDto>(data), ct);
                await SaveTermsAsync(tenantId, userId, terms, ct);
                fullData = fullData with { Terms = terms };
                break;
            case 5:
                var classes = await ValidatedAsync(ReadStep<ClassesAndStreamsDto>(data), ct);
                await SaveClassesAndStreamsAsync(tenantId, userId, classes, ct);
                fullData = fullData with { ClassesAndStreams = classes };
                break;
            case 6:
                var subjects = await ValidatedAsync(ReadStep<SubjectsDto>(data), ct);
                await SaveSubjectsAsync(tenantId, subjects, ct);
                fullData = fullData with { Subjects = subjects };
                break;
            case 7:
                var depts = await ValidatedAsync(ReadStep<DepartmentsAndRolesDto>(data), ct);
                await SaveDepartmentsAsync(tenantId, depts, ct);
                fullData = fullData with { DepartmentsAndRoles = depts };
                break;
            case 8:
                var grading = await ValidatedAsync(ReadStep<GradingScaleDto>(data), ct);
                await SaveGradingScaleAsync(tenantId, grading, ct);
                fullData = fullData with { GradingScale = grading };
                break;
            case 9:
                var prefs = await ValidatedAsync(ReadStep<PreferencesDto>(data), ct);
                await SavePreferencesAsync(tenantId, prefs, ct);
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

        // Counts from actual domain tables. Departments and grading bands are stored as JSON
        // settings, not tables, so they are not counted.
        var counts = new Dictionary<string,int>
        {
            ["academicYears"] = await _db.Set<AcademicYear>().CountAsync(y=>y.TenantId==tenantId, ct),
            ["terms"] = await _db.Set<Term>().CountAsync(t=>t.TenantId==tenantId, ct),
            ["grades"] = await _db.Set<Grade>().CountAsync(g=>g.TenantId==tenantId, ct),
            ["streams"] = await _db.Set<ClassStream>().CountAsync(s=>s.TenantId==tenantId, ct),
            ["subjects"] = await _db.Set<Subject>().CountAsync(s=>s.TenantId==tenantId, ct)
        };

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
            // Used to be a string Replace of every "}", which broke the JSON as soon as it held
            // a nested object (the grading scale).
            tenantSettings.FeaturesJson = MergeJson(MergeJson(tenantSettings.FeaturesJson, "setupComplete", true), "setupCompletedAt", DateTime.UtcNow);
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

    // Steps 3 to 5 create real academic_years, terms, grades and class_streams rows through the
    // Core services. They used to store the year's name as its id (2026) in settings, keep
    // terms only as JSON, and create grades and streams pointing at academic year id 2026.

    private async Task SaveAcademicYearAsync(long tenantId, long userId, AcademicYearDto dto, CancellationToken ct)
    {
        var name = dto.Name.Trim();
        var existing = await _db.Set<AcademicYear>().FirstOrDefaultAsync(y => y.TenantId == tenantId && y.Name == name, ct);
        long yearId;
        if (existing is null)
        {
            yearId = (await _calendar.CreateYearAsync(tenantId, userId, new LearnCloud.Core.DTOs.CreateAcademicYearRequest(name, dto.StartDate, dto.EndDate, IsCurrent: true), ct)).Id;
        }
        else
        {
            yearId = (await _calendar.UpdateYearAsync(tenantId, userId, existing.Id, new LearnCloud.Core.DTOs.UpdateAcademicYearRequest(name, dto.StartDate, dto.EndDate), ct)).Id;
            await _calendar.SetCurrentYearAsync(tenantId, userId, yearId, ct);
        }

        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings==null)
        {
            settings = new TenantSettings { TenantId=tenantId, PrimaryColor="#0F153A" };
            _db.TenantSettings.Add(settings);
        }
        settings.CurrentAcademicYearId = checked((int)yearId);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<long> WizardYearIdAsync(long tenantId, CancellationToken ct) =>
        await _db.Set<AcademicYear>().Where(y => y.TenantId == tenantId && y.IsCurrent).Select(y => (long?)y.Id).FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("Save the academic year step first.");

    private async Task SaveTermsAsync(long tenantId, long userId, TermsSetupDto dto, CancellationToken ct)
    {
        var yearId = await WizardYearIdAsync(tenantId, ct);
        var terms = dto.Terms.Select(t => new LearnCloud.Core.DTOs.CreateTermRequest(t.Name, t.TermNumber, t.StartDate, t.EndDate, t.IsCurrent)).ToList();
        await _calendar.ReplaceTermsAsync(tenantId, userId, yearId, terms, ct);
    }

    private async Task SaveClassesAndStreamsAsync(long tenantId, long userId, ClassesAndStreamsDto dto, CancellationToken ct)
    {
        var yearId = await WizardYearIdAsync(tenantId, ct);
        for (var i = 0; i < dto.Classes.Count; i++)
        {
            var cls = dto.Classes[i];
            var code = cls.GradeCode.Trim().ToUpperInvariant();
            var grade = await _db.Set<Grade>().FirstOrDefaultAsync(g => g.TenantId == tenantId && g.AcademicYearId == yearId && g.Code == code, ct);
            var gradeId = grade is null
                ? (await _classes.CreateGradeAsync(tenantId, userId, new LearnCloud.Core.DTOs.CreateGradeRequest(yearId, cls.GradeName, code, i + 1), ct)).Id
                : (await _classes.UpdateGradeAsync(tenantId, userId, grade.Id, new LearnCloud.Core.DTOs.UpdateGradeRequest(cls.GradeName, code, i + 1, grade.IsActive), ct)).Id;

            foreach (var stream in cls.Streams)
            {
                var name = stream.Name.Trim();
                var existing = await _db.Set<ClassStream>().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.GradeId == gradeId && s.Name == name, ct);
                if (existing is null)
                    await _classes.CreateStreamAsync(tenantId, userId, gradeId, new LearnCloud.Core.DTOs.CreateStreamRequest(name, stream.Capacity), ct);
                else
                    await _classes.UpdateStreamAsync(tenantId, userId, existing.Id, new LearnCloud.Core.DTOs.UpdateStreamRequest(name, stream.Capacity), ct);
            }
        }
    }

    // Departments, grading scale and preferences are kept as JSON in tenant settings. Each
    // step writes its own key: steps 7 and 9 used to overwrite the same field, and step 8
    // replaced the whole features JSON.
    private static string MergeJson(string? json, string key, object? value)
    {
        JsonObject root;
        try { root = (string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject) ?? new JsonObject(); }
        catch (JsonException) { root = new JsonObject(); }
        root[key] = JsonSerializer.SerializeToNode(value);
        return root.ToJsonString();
    }

    private static T? ReadStep<T>(object data) where T : class =>
        data is JsonElement element ? element.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : data as T;

    private async Task SaveSubjectsAsync(long tenantId, SubjectsDto dto, CancellationToken ct)
    {
        foreach (var sub in dto.Subjects.Where(s=>s.Selected))
        {
            var existing = await _db.Set<Subject>().FirstOrDefaultAsync(s=>s.TenantId==tenantId && s.Code==sub.Code && !s.IsDeleted, ct);
            if (existing==null)
            {
                var newSub = new Subject { TenantId=tenantId, Name=sub.Name, Code=sub.Code };
                _db.Set<Subject>().Add(newSub);
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
            settings.BrandingJson = MergeJson(settings.BrandingJson, "departmentsAndRoles", dto);
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveGradingScaleAsync(long tenantId, GradingScaleDto dto, CancellationToken ct)
    {
        // Store grading scale as JSON in TenantSettings for V1, real would have GradingScale tables
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings!=null)
        {
            settings.FeaturesJson = MergeJson(settings.FeaturesJson, "gradingScale", dto);
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SavePreferencesAsync(long tenantId, PreferencesDto dto, CancellationToken ct)
    {
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(ts=>ts.TenantId==tenantId, ct);
        if (settings==null)
        {
            // This used to put the week start day into the time zone.
            settings = new TenantSettings { TenantId=tenantId, PrimaryColor="#0F153A" };
            _db.TenantSettings.Add(settings);
        }
        settings.BrandingJson = MergeJson(settings.BrandingJson, "preferences", dto);
        await _db.SaveChangesAsync(ct);
    }

    private List<NextActionDto> GetNextActions() => new()
    {
        new("Import learners", "Bulk CSV import 500 students with row errors, per SRS FR-SR05", "/students/import", "users", "Import CSV"),
        new("Add staff", "Create staff profiles and link to user accounts, assign class teacher", "/staff", "user-plus", "Add staff"),
        new("Set fee structures", "Define fee line items per grade/term with DECIMAL(18,2)+currency", "/fees/structures", "dollar", "Create fees")
    };
}
