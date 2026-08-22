using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.TeacherPortal.Entities;

// Homework and assignments: create, attach file, set due date, target class, see submission status
public class HomeworkAssignment : TenantOwnedEntity
{
    public long TeacherStaffId { get; set; } // who created
    public long SubjectId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; } // targeted class
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }

    public string Title { get; set; } = null!;
    public string Description { get; set; } = "";
    public string? FileUrl { get; set; } // attached file
    public string? FileName { get; set; }
    public long? FileSize { get; set; }

    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "active"; // active, archived, draft

    // Navigation
    public ICollection<HomeworkSubmission> Submissions { get; set; } = new List<HomeworkSubmission>();
}

public class HomeworkSubmission : TenantOwnedEntity
{
    public long AssignmentId { get; set; }
    public HomeworkAssignment Assignment { get; set; } = null!;

    public long StudentId { get; set; }
    public string Status { get; set; } = "pending"; // pending, submitted, late, not_submitted
    public DateTime? SubmittedAt { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? Note { get; set; }
    public string? TeacherFeedback { get; set; }
    public decimal? Score { get; set; }
}

// Lesson plans: create against subject and class, simple template
public class LessonPlan : TenantOwnedEntity
{
    public long TeacherStaffId { get; set; }
    public long SubjectId { get; set; }
    public long GradeId { get; set; }
    public long StreamId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }

    public DateTime Date { get; set; } // planned date

    // Simple template fields
    public string Objective { get; set; } = null!; // What learners will achieve
    public string Activities { get; set; } = null!; // Introduction, Main, Conclusion
    public string Resources { get; set; } = ""; // Materials needed
    public string Assessment { get; set; } = ""; // How to assess
    public string Reflection { get; set; } = ""; // After lesson

    public string Status { get; set; } = "draft"; // draft, planned, taught, archived
}

// For dashboard aggregates - not tables, but DTO will compute
public class TeacherNotice : TenantOwnedEntity
{
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string Priority { get; set; } = "normal"; // low, normal, high, urgent
    public bool IsRead { get; set; } = false;
    public long? TargetTeacherStaffId { get; set; } // null = all teachers
    public long? TargetGradeId { get; set; }
    public long? TargetStreamId { get; set; }
}
