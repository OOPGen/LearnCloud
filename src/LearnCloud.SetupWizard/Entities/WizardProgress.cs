using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.SetupWizard.Entities;

public enum WizardStep
{
    SchoolProfile = 1,
    Branding = 2,
    AcademicYear = 3,
    Terms = 4,
    ClassesAndStreams = 5,
    Subjects = 6,
    DepartmentsAndRoles = 7,
    GradingScale = 8,
    Preferences = 9,
    Complete = 10
}

public enum StepStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3
}

public class WizardProgress : TenantOwnedEntity
{
    public int CurrentStep { get; set; } = 1; // 1-9
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;

    // JSON blobs for each step status: {"1":"completed","2":"skipped",...}
    public string StepsStatusJson { get; set; } = "{\"1\":\"not_started\"}";

    // Full data of wizard for resume - JSON containing all steps data
    public string? DataJson { get; set; }

    // Counts for completion summary
    public string? SummaryJson { get; set; }

    // Helper to get/set status via code - not mapped
}

public class WizardStepData
{
    public int Step { get; set; }
    public StepStatus Status { get; set; }
    public DateTime SavedAt { get; set; }
    public object? Data { get; set; }
}
