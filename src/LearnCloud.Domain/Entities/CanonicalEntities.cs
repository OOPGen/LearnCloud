using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Domain.Entities;

// Canonical Student entity - single source of truth, replaces 12+ duplicate definitions
public class Student : TenantOwnedEntity
{
    public string StudentNumber { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public DateTime? Dob { get; set; }
    public string? Gender { get; set; }
    public string? NationalId { get; set; }
    public string? PhotoUrl { get; set; }
    public string Status { get; set; } = "active"; // applicant, active, inactive, alumni, transferred
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public long AcademicYearId { get; set; }
    public long? CurrentEnrolmentId { get; set; }
    public long? UserId { get; set; } // for student portal login
}

public class Grade : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // Grade 5, Form 1
    public string Code { get; set; } = null!; // G5, F1
    public int LevelOrder { get; set; }
    public long AcademicYearId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Stream : TenantOwnedEntity
{
    public long GradeId { get; set; }
    public string Name { get; set; } = null!; // Blue, Green
    public string FullName => $"{GradeId} {Name}"; // computed in service with grade name
    public int Capacity { get; set; } = 40;
    public long? ClassTeacherStaffId { get; set; }
    public long? RoomId { get; set; }
    public long AcademicYearId { get; set; }
}

public class Subject : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // Mathematics
    public string Code { get; set; } = null!; // MATH
    public string? Description { get; set; }
    public bool IsCore { get; set; } = true;
    public string? Department { get; set; }
    public string Status { get; set; } = "active";
}

public class Guardian : TenantOwnedEntity
{
    public long? UserId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? NationalId { get; set; }
}

public class GuardianStudentLink : TenantOwnedEntity
{
    public long GuardianId { get; set; }
    public long StudentId { get; set; }
    public string RelationshipType { get; set; } = "guardian"; // mother, father, guardian, aunt...
    public bool IsPrimaryContact { get; set; } = false;
    public bool IsBillingContact { get; set; } = false;
    public bool IsEmergencyContact { get; set; } = false;
    public bool CanPickup { get; set; } = true;
    public long AcademicYearId { get; set; }
}

public class Staff : TenantOwnedEntity
{
    public long? UserId { get; set; }
    public string StaffNumber { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public string EmploymentType { get; set; } = "permanent"; // permanent, contract, part_time
    public string? Qualification { get; set; }
    public string? NationalId { get; set; }
    public DateTime? HireDate { get; set; }
    public string Status { get; set; } = "active";
}

public class StudentEnrolment : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public string EnrolmentStatus { get; set; } = "enrolled"; // enrolled, promoted, repeated, transferred_in/out, withdrawn, graduated, readmitted
    public string EnrolmentType { get; set; } = "new"; // new, repeat, transfer, readmission, continuing
    public long? PreviousEnrolmentId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExitDate { get; set; }
    public bool IsCurrent { get; set; } = true;
}

public class Term : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public string Name { get; set; } = null!; // Term 1, Term 2
    public int TermNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; } = false;
}

public class Room : TenantOwnedEntity
{
    public string Name { get; set; } = null!;
    public string? Building { get; set; }
    public int? Capacity { get; set; }
    public string RoomType { get; set; } = "classroom"; // classroom, lab, library, hall
}

public class SubjectGradeLink : TenantOwnedEntity
{
    public long GradeId { get; set; }
    public long SubjectId { get; set; }
    public long AcademicYearId { get; set; }
    public bool IsCompulsory { get; set; } = true;
}
