using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Domain.Entities;

// Academic records: marks, report cards and the academic calendar.
//
// These were previously declared as one-line stubs at the bottom of
// StudentPortalService.cs, or (StudentMark, Assessment, AcademicYear) not declared at all
// while several modules queried them. They are canonical domain records read by
// AI, Examinations, TeacherPortal, StudentPortal and ParentPortal, so they live
// here alongside Student and Grade.
//
// Field sets are reconstructed from how the querying modules use them. Review
// against LearnCloud_Database_Schema_v1.md before generating a migration.

public class AcademicYear : TenantOwnedEntity
{
    public string Name { get; set; } = null!;          // 2026
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; } = false;
}

public class Assessment : TenantOwnedEntity
{
    public string Name { get; set; } = null!;          // Mid-term test, Assignment 2
    public long SubjectId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public DateTime? AssessmentDate { get; set; }      // null until scheduled
    public decimal MaxScore { get; set; } = 100m;
    public decimal Weighting { get; set; } = 1m;       // contribution to the term composite
}

public class StudentMark : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long SubjectId { get; set; }
    public long GradeId { get; set; }                  // denormalised from the assessment for class-level queries
    public long StreamId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long? AssessmentId { get; set; }            // null for a standalone mark
    public decimal? Score { get; set; }                // null until entered
    public decimal MaxScore { get; set; } = 100m;
    public string? GradeLetter { get; set; }
    public long? EnteredByStaffId { get; set; }
}

public class ReportCard : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public decimal TotalAverage { get; set; }
    public string? OverallGradeLetter { get; set; }
    public int? ClassRank { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PdfUrl { get; set; }
    public string? ClassTeacherComment { get; set; }
    public string? HeadComment { get; set; }
    public DateTime? NextTermStartDate { get; set; }
    public string Status { get; set; } = "draft";      // draft, published
}

public class ReportCardSubject : TenantOwnedEntity
{
    public long ReportCardId { get; set; }
    public long SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public decimal? Score { get; set; }
    public decimal MaxScore { get; set; }
    public string? GradeLetter { get; set; }
    public string? TeacherComment { get; set; }
    public decimal? ClassAverage { get; set; }
}
