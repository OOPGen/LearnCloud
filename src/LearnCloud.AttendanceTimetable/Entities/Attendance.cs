using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.AttendanceTimetable.Entities;

public enum AttendanceMode
{
    Daily = 1,      // one record per student per day per class
    PerPeriod = 2   // one record per student per day per period per class
}

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3,
    Sick = 4,
    Excused = 5
}

// Tenant-level settings for attendance - configured per tenant
public class TenantAttendanceSettings : TenantOwnedEntity
{
    public AttendanceMode Mode { get; set; } = AttendanceMode.Daily;
    public int BackdatingWindowDays { get; set; } = 7; // allow backdating within X days, else requires admin override
    public bool AllowBackdatingBeyondWindow { get; set; } = true; // if true, allowed but flagged in audit
    public decimal ChronicAbsenceThreshold { get; set; } = 85m; // % below flags chronic
    public bool CountLateAsPresent { get; set; } = true;
    public bool CountExcusedAsPresent { get; set; } = true;
    public bool CountSickAsPresent { get; set; } = false;
    public int AutoSaveIntervalSeconds { get; set; } = 5;
}

// Register header - groups records for same class/date/period, guards duplicate
public class AttendanceRegister : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public DateTime AttendanceDate { get; set; } // Date only
    public int? PeriodNumber { get; set; } // null for daily mode
    public string Status { get; set; } = "draft"; // draft, submitted, locked
    public bool IsBackdated { get; set; } = false;
    public DateTime? SubmittedAt { get; set; }
    public long? SubmittedByUserId { get; set; }

    // Navigation
    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}

// Per-learner record - optimized for speed capture
public class AttendanceRecord : TenantOwnedEntity
{
    public long RegisterId { get; set; }
    public AttendanceRegister Register { get; set; } = null!;

    public long StudentId { get; set; }
    // Denormalized for fast filtering per spec: every index leads with tenant_id
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }

    public DateTime AttendanceDate { get; set; }
    public int? PeriodNumber { get; set; } // null for daily

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    public string? AbsenceReason { get; set; } // sick, family, transport, other - plain language
    public string? Note { get; set; } // optional note max 255

    public long MarkedByUserId { get; set; }
    public DateTime MarkedAt { get; set; } = DateTime.UtcNow;

    public bool IsBackdated { get; set; } = false;
    public string? BackdateReason { get; set; }
}

// For summary caching (optional, could be computed via query)
public class AttendanceSummaryCache : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long? GradeId { get; set; }
    public long? StreamId { get; set; }
    public int TotalDays { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int LateCount { get; set; }
    public int ExcusedCount { get; set; }
    public int SickCount { get; set; }
    public decimal Percentage { get; set; } // calculated
    public bool IsChronicAbsence { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
