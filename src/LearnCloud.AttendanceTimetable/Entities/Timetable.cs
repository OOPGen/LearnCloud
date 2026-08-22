using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.AttendanceTimetable.Entities;

// Period definition per tenant with times and break slots
public class PeriodDefinition : TenantOwnedEntity
{
    public int PeriodNumber { get; set; } // 1..12
    public string Name { get; set; } = null!; // Period 1, Break, Period 2
    public TimeSpan StartTime { get; set; } // 08:00
    public TimeSpan EndTime { get; set; } // 08:45
    public bool IsBreak { get; set; } = false;
    public int SortOrder { get; set; }
    public long? AcademicYearId { get; set; } // null = global for tenant, or per year
}

public class BreakSlot : TenantOwnedEntity
{
    public string Name { get; set; } = "Break"; // Short break, Lunch
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int SortOrder { get; set; }
}

// Effective dated timetable - mid-term change does not rewrite history
public class Timetable : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // Term 2 2026 Timetable v1
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public DateTime EffectiveFrom { get; set; } // inclusive
    public DateTime? EffectiveTo { get; set; } // inclusive, null = ongoing
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "draft"; // draft, active, archived
    public long? CreatedFromTimetableId { get; set; } // clone source

    public ICollection<TimetableSlot> Slots { get; set; } = new List<TimetableSlot>();
}

// Weekly grid slot assigning subject and teacher to a class period
public class TimetableSlot : TenantOwnedEntity
{
    public long TimetableId { get; set; }
    public Timetable Timetable { get; set; } = null!;

    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; } // class

    public long SubjectId { get; set; }
    public long TeacherStaffId { get; set; } // teacher
    public long? RoomId { get; set; } // optional room

    public int DayOfWeek { get; set; } // 1=Mon .. 7=Sun, per spec DayOfWeek BETWEEN 1 AND 7
    public int PeriodNumber { get; set; } // 1..12, FK to PeriodDefinition

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    // For quick conflict detection index: tenant_id leading
    // Unique constraints: (tenant_id, timetable_id, day_of_week, period_number, stream_id) no double-book class
    // (tenant_id, timetable_id, day_of_week, period_number, teacher_staff_id) no teacher in two places
    // (tenant_id, timetable_id, day_of_week, period_number, room_id) room conflict
}

// Clash result for plain language explanation
public class TimetableClash
{
    public string ClashType { get; set; } = null!; // Teacher, Class, Room
    public string Message { get; set; } = null!; // plain language
    public long ExistingSlotId { get; set; }
    public long NewSlotId { get; set; }
    public long TeacherStaffId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public long? RoomId { get; set; }
    public int DayOfWeek { get; set; }
    public int PeriodNumber { get; set; }
    public string ExistingInfo { get; set; } = null!; // e.g. "Form 1A Math with Mrs Moyo in Room 5"
    public string NewInfo { get; set; } = null!;
}
