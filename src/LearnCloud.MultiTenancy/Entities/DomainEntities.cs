namespace LearnCloud.MultiTenancy.Entities;

// C2 CLEANUP: All domain entities (Student, Guardian, Grade, Stream, Subject, Fee*, Assessment etc.)
// have been moved to their canonical locations:
// - LearnCloud.Domain.Entities for Student, Grade, Stream, Subject, Guardian, GuardianStudentLink, Staff, StudentEnrolment, Term, Room, SubjectGradeLink
// - LearnCloud.Fees.Entities for FeeItem, FeeStructure, FeeInvoice, Payment etc.
// - LearnCloud.AttendanceTimetable for AttendanceRecord, TimetableSlot
// - LearnCloud.Examinations for Assessment, StudentMark
// - LearnCloud.Messaging for Message
// This file now contains ONLY infrastructure-level AuditLog which is platform-wide.

// Audit log - stores tenant, actor, entity, action, before/after - PLATFORM INFRASTRUCTURE
public class AuditLog : BaseEntity
{
    public long? TenantId { get; set; } // nullable for platform events
    public long? UserId { get; set; }
    public string EntityType { get; set; } = null!; // e.g. Student
    public long EntityId { get; set; }
    public string Action { get; set; } = null!; // create, update, delete, soft_delete, restore, view_sensitive
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public long? AcademicYearId { get; set; }
    public long? TermId { get; set; }
    public string? Reason { get; set; } // for explicit no-tenant
}
