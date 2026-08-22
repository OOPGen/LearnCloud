using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Analytics.Entities;

// Pre-aggregate on a schedule rather than computing on request - tables store aggregates

// Executive dashboard aggregates
public class DailyAggregate : TenantOwnedEntity
{
    public DateTime Date { get; set; } // 2026-08-03
    public int AcademicYearId { get; set; }
    public long TermId { get; set; }
    public int EnrolmentCount { get; set; }
    public int ActiveStudents { get; set; }
    public decimal FeeCollectionRate { get; set; } // collection rate %
    public decimal AverageAttendance { get; set; } // %
    public decimal AveragePerformance { get; set; } // %
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalArrears { get; set; }
}

public class ClassAggregate : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public string GradeName { get; set; } = "";
    public string StreamName { get; set; } = "";
    public int EnrolmentCount { get; set; }
    public decimal AverageAttendance { get; set; }
    public decimal AveragePerformance { get; set; }
    public decimal FeeCollectionRate { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
}

public class StreamAggregate : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long StreamId { get; set; }
    public string StreamName { get; set; } = "";
    public int EnrolmentCount { get; set; }
    public decimal AverageAttendance { get; set; }
    public decimal AveragePerformance { get; set; }
}

public class SubjectAggregate : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public decimal AverageScore { get; set; }
    public int AssessmentCount { get; set; }
    public int StudentCount { get; set; }
}

public class TeacherAggregate : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long TeacherStaffId { get; set; }
    public string TeacherName { get; set; } = "";
    public decimal AverageAttendance { get; set; } // average attendance of classes they teach
    public decimal AveragePerformance { get; set; } // average performance of learners they teach
    public int ClassesTaught { get; set; }
    public int TotalLearners { get; set; }
}

public class TermComparison : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long CurrentTermId { get; set; }
    public long PreviousTermId { get; set; }
    public string Metric { get; set; } = ""; // enrolment, fee_collection_rate, attendance, performance
    public decimal CurrentValue { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal Change { get; set; } // current - previous
    public decimal ChangePercent { get; set; } // change / previous *100
}

public class YearComparison : TenantOwnedEntity
{
    public long CurrentAcademicYearId { get; set; }
    public long PreviousAcademicYearId { get; set; }
    public long TermId { get; set; }
    public string Metric { get; set; } = "";
    public decimal CurrentValue { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
}

// Cohort view following a year group through the school
public class CohortSnapshot : TenantOwnedEntity
{
    public long OriginalGradeId { get; set; } // e.g. Grade 1 in 2020
    public string OriginalGradeName { get; set; } = "";
    public long OriginalAcademicYearId { get; set; } // e.g. 2020
    public long CurrentGradeId { get; set; } // e.g. Grade 5 in 2024
    public string CurrentGradeName { get; set; } = "";
    public long CurrentAcademicYearId { get; set; }
    public int OriginalEnrolment { get; set; }
    public int CurrentEnrolment { get; set; }
    public int Retained { get; set; } // still in school
    public int Left { get; set; }
    public decimal RetentionRate { get; set; }
    public decimal AveragePerformance { get; set; }
    public decimal AverageAttendance { get; set; }
}

// Fee collection analysis by class and by payment method
public class FeeCollectionByClass : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public string GradeName { get; set; } = "";
    public string StreamName { get; set; } = "";
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalArrears { get; set; }
    public decimal CollectionRate { get; set; }
    public int LearnerCount { get; set; }
}

public class FeeCollectionByPaymentMethod : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public string PaymentMethod { get; set; } = ""; // cash, bank_transfer, ecocash, card
    public decimal TotalCollected { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageAmount { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

// For group customers, consolidated cross-school view
public class SchoolGroup : BaseEntity
{
    public string Name { get; set; } = null!; // e.g. Petra Schools Group
    public string Code { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public ICollection<SchoolGroupMembership> Memberships { get; set; } = new List<SchoolGroupMembership>();
}

public class SchoolGroupMembership : BaseEntity
{
    public long SchoolGroupId { get; set; }
    public SchoolGroup Group { get; set; } = null!;
    public long TenantId { get; set; } // school tenant owned by group
    public Tenant Tenant { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class GroupAggregate : BaseEntity
{
    public long SchoolGroupId { get; set; }
    public SchoolGroup Group { get; set; } = null!;
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public DateTime Date { get; set; }
    public int TotalEnrolment { get; set; }
    public decimal AverageFeeCollectionRate { get; set; }
    public decimal AverageAttendance { get; set; }
    public decimal AveragePerformance { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public int SchoolCount { get; set; }
}

public class GroupSchoolComparison : BaseEntity
{
    public long SchoolGroupId { get; set; }
    public long TenantId { get; set; }
    public string TenantName { get; set; } = "";
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public int EnrolmentCount { get; set; }
    public decimal FeeCollectionRate { get; set; }
    public decimal AverageAttendance { get; set; }
    public decimal AveragePerformance { get; set; }
    public int RankByEnrolment { get; set; }
    public int RankByFeeCollection { get; set; }
    public int RankByAttendance { get; set; }
    public int RankByPerformance { get; set; }
}
