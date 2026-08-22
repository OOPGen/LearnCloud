using System.ComponentModel.DataAnnotations;

namespace LearnCloud.SetupWizard.DTOs;

// Step 1: School Profile
public record SchoolProfileDto(
    string Name,
    string Type, // primary, secondary, combined, early_years
    string Address,
    string City,
    string Country,
    string ContactEmail,
    string ContactPhone,
    string Timezone, // Africa/Harare
    string BaseCurrency, // USD
    bool ZWGCurrencyEnabled,
    string LearnerCountBand
);

// Step 2: Branding
public record BrandingDto(
    string? LogoUrl, // after upload
    string PrimaryColor, // #0F153A
    string SecondaryColor, // #5F3F96
    string? LogoBase64 // for upload alternative
);

// Step 3: Academic Year
public record AcademicYearDto(
    string Name, // 2026
    DateTime StartDate,
    DateTime EndDate,
    bool IsCurrent
);

// Step 4: Terms
public record TermDto(
    string Name, // Term 1
    int TermNumber,
    DateTime StartDate,
    DateTime EndDate,
    bool IsCurrent
);
public record TermsSetupDto(
    int Count, // default 3
    List<TermDto> Terms
);

// Step 5: Classes and Streams - bulk entry e.g. Form 1: A,B,C
public record StreamInputDto(string Name, int Capacity);
public record ClassInputDto(string GradeName, string GradeCode, List<StreamInputDto> Streams);
public record ClassesAndStreamsDto(List<ClassInputDto> Classes);

// Step 6: Subjects - starter list accept/edit
public record SubjectInputDto(string Name, string Code, bool IsCore, bool Selected);
public record SubjectsDto(List<SubjectInputDto> Subjects);

// Step 7: Departments and Staff Roles
public record DepartmentInputDto(string Name, string? HodName, string? Description);
public record DepartmentsAndRolesDto(
    List<DepartmentInputDto> Departments,
    List<string> CustomRoles // e.g. Lab Technician beyond default roles
);

// Step 8: Grading Scale
public record GradingBandDto(string Symbol, string Description, decimal MinScore, decimal MaxScore, decimal? GradePoint, string? Color);
public record GradingScaleDto(string Name, bool IsDefault, List<GradingBandDto> Bands);

// Step 9: Preferences
public record PreferencesDto(
    string WeekStart, // Monday, Sunday
    string AttendanceMode, // daily, per_period
    string InvoiceNumberPrefix, // INV
    int InvoiceNextNumber, // 1
    string InvoiceNumberFormat, // {prefix}-{year}-{number:5}
    bool EnableParentPortal,
    bool EnableSmsNotifications
);

// Progress & Summary
public record WizardProgressDto(
    int CurrentStep,
    Dictionary<int,string> StepsStatus, // step -> status string
    bool IsCompleted,
    DateTime LastSavedAt,
    DateTime? CompletedAt,
    object? CurrentData // resume data for current step
);

public record WizardSummaryDto(
    bool IsCompleted,
    int TotalStepsCompleted,
    int TotalStepsSkipped,
    Dictionary<string,int> Counts, // e.g. grades:5, streams:12, subjects:10, departments:3, gradingBands:6
    List<string> CreatedEntities,
    DateTime CompletedAt,
    List<NextActionDto> NextActions
);

public record NextActionDto(string Title, string Description, string ActionUrl, string Icon, string Cta);

// Full wizard data for resume
public record FullWizardDataDto(
    SchoolProfileDto? SchoolProfile,
    BrandingDto? Branding,
    AcademicYearDto? AcademicYear,
    TermsSetupDto? Terms,
    ClassesAndStreamsDto? ClassesAndStreams,
    SubjectsDto? Subjects,
    DepartmentsAndRolesDto? DepartmentsAndRoles,
    GradingScaleDto? GradingScale,
    PreferencesDto? Preferences
);

// Upload logo response
public record LogoUploadResponse(string LogoUrl, string Message);

// Generic step save request
public record SaveStepRequest<T>(int Step, T Data);
