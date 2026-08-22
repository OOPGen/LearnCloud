using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.HR.Entities;

// Staff records - extends existing StaffProfile
public class Staff : TenantOwnedEntity
{
    public string StaffNumber { get; set; } = null!; // STA-2026-00001
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? NationalId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = "other"; // M/F/other
    public string EmploymentType { get; set; } = "permanent"; // permanent, contract, part_time, temporary
    public string EmploymentStatus { get; set; } = "active"; // active, on_leave, suspended, terminated, retired
    public long? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Designation { get; set; } // e.g. Mathematics Teacher, Bursar, Head
    public DateTime HireDate { get; set; }
    public DateTime? ConfirmationDate { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public long? UserId { get; set; } // linked user account
    public string? PhotoUrl { get; set; }
    public decimal? CurrentSalary { get; set; } // for HR reference, not payroll calculation
    public string Currency { get; set; } = "USD";

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<Qualification> Qualifications { get; set; } = new List<Qualification>();
    public ICollection<StaffDocument> Documents { get; set; } = new List<StaffDocument>();
}

// Contracts with expiry reminders
public class Contract : TenantOwnedEntity
{
    public long StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public string ContractNumber { get; set; } = null!; // CONT-2026-00001
    public string ContractType { get; set; } = "permanent"; // permanent, fixed_term, probation
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; } // for fixed term
    public DateTime? ProbationEndDate { get; set; }
    public decimal? Salary { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "active"; // draft, active, expired, terminated, renewed
    public string? Terms { get; set; }
    public DateTime? LastReminderSentAt { get; set; }
    public long CreatedByUserId { get; set; }
}

// Qualifications and documents
public class Qualification : TenantOwnedEntity
{
    public long StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public string QualificationName { get; set; } = null!; // e.g. B.Ed Mathematics
    public string Institution { get; set; } = null!; // University of Zimbabwe
    public int? YearObtained { get; set; }
    public string? Grade { get; set; }
    public string? CertificateNumber { get; set; }
    public bool IsVerified { get; set; } = false;
    public long? VerifiedByUserId { get; set; }
}

public class StaffDocument : TenantOwnedEntity
{
    public long StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public string DocumentType { get; set; } = null!; // national_id, contract, qualification_certificate, police_clearance, medical, etc.
    public string FileName { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public DateTime? ExpiryDate { get; set; } // for police clearance, etc.
    public bool IsVerified { get; set; } = false;
    public long UploadedByUserId { get; set; }
}

public class Department : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // Sciences, Commercials, Administration
    public string Code { get; set; } = null!;
    public long? HodStaffId { get; set; }
    public bool IsActive { get; set; } = true;
}

// Leave types with entitlements
public class LeaveType : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // Annual, Sick, Maternity, Study, Compassionate, Unpaid
    public string Code { get; set; } = null!; // ANNUAL, SICK, MATERNITY
    public string Description { get; set; } = "";
    public int DefaultEntitlementDays { get; set; } // e.g. 22 days annual
    public bool IsPaid { get; set; } = true;
    public bool RequiresDocument { get; set; } = false; // sick leave requires medical cert
    public bool IsCarryForwardAllowed { get; set; } = false;
    public int MaxCarryForwardDays { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string AccrualRule { get; set; } = "yearly"; // yearly, monthly, none
}

public class LeaveEntitlement : TenantOwnedEntity
{
    public long StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public long LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;
    public int AcademicYear { get; set; } // e.g. 2026
    public decimal EntitledDays { get; set; } // e.g. 22 days, may be prorated for mid-year joiners
    public decimal CarriedForwardDays { get; set; } = 0m;
    public decimal UsedDays { get; set; } = 0m;
    public decimal RemainingDays { get; set; } // Entitled + Carried - Used
    public DateTime? ExpiryDate { get; set; } // carry forward expiry
}

// Leave request and approval workflow with balance calculation and leave calendar
public class LeaveRequest : TenantOwnedEntity
{
    public long StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public long LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DaysRequested { get; set; } // calculated excluding weekends/holidays
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = "pending"; // pending, approved, rejected, cancelled, withdrawn
    public long? ApproverUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApproverComment { get; set; }
    public string? DocumentUrl { get; set; } // medical cert etc.
    public long RequestedByUserId { get; set; }
    public bool IsHalfDay { get; set; } = false;
}

// Appraisal cycles with configurable criteria
public class AppraisalCycle : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. 2026 Mid-Year Appraisal
    public long AcademicYearId { get; set; }
    public long? TermId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "draft"; // draft, active, closed
    public string? Description { get; set; }
    public ICollection<AppraisalCriterion> Criteria { get; set; } = new List<AppraisalCriterion>();
}

public class AppraisalCriterion : TenantOwnedEntity
{
    public long AppraisalCycleId { get; set; }
    public AppraisalCycle Cycle { get; set; } = null!;
    public string Name { get; set; } = null!; // e.g. Classroom Management, Subject Knowledge, Punctuality
    public string? Description { get; set; }
    public int Weight { get; set; } = 1; // weighting
    public int MaxScore { get; set; } = 5; // e.g. 1-5 scale
    public int SortOrder { get; set; }
}

public class Appraisal : TenantOwnedEntity
{
    public long AppraisalCycleId { get; set; }
    public AppraisalCycle Cycle { get; set; } = null!;
    public long StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public long AppraiserUserId { get; set; } // who appraised
    public string Status { get; set; } = "draft"; // draft, submitted, acknowledged, closed
    public decimal OverallScore { get; set; }
    public string? OverallComment { get; set; }
    public string? StaffComment { get; set; } // staff self-comment / acknowledgment
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    public ICollection<AppraisalScore> Scores { get; set; } = new List<AppraisalScore>();
}

public class AppraisalScore : TenantOwnedEntity
{
    public long AppraisalId { get; set; }
    public Appraisal Appraisal { get; set; } = null!;
    public long CriterionId { get; set; }
    public AppraisalCriterion Criterion { get; set; } = null!;
    public int Score { get; set; } // 1-5
    public string? Comment { get; set; }
}

// Disciplinary records with restricted access
public class DisciplinaryRecord : TenantOwnedEntity
{
    public long StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public DateTime IncidentDate { get; set; }
    public string IncidentType { get; set; } = null!; // misconduct, absenteeism, negligence, etc.
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Severity { get; set; } = "low"; // low, medium, high, critical
    public string ActionTaken { get; set; } = null!; // verbal warning, written warning, suspension, etc.
    public string Status { get; set; } = "open"; // open, under_review, resolved, closed, appealed
    public long ReportedByUserId { get; set; }
    public long? AssignedToUserId { get; set; }
    public string Visibility { get; set; } = "hr_only"; // hr_only, head_only, admin_only - restricted access
    public bool IsConfidential { get; set; } = true;
    public DateTime? ResolutionDate { get; set; }
    public string? ResolutionNotes { get; set; }
}
