using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Examinations.Entities;

// Examination sessions grouping assessments across subjects for a term, with timetable, venues and invigilator assignment
public class ExaminationSession : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // Term 2 2026 Final Exams
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public string SessionType { get; set; } = "final"; // mid_term, final, mock, supplementary
    public string Status { get; set; } = "draft"; // draft, scheduled, ongoing, completed, archived
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Description { get; set; }

    public ICollection<ExaminationSlot> Slots { get; set; } = new List<ExaminationSlot>();
    public ICollection<ExaminationSessionAssessment> Assessments { get; set; } = new List<ExaminationSessionAssessment>();
}

public class ExaminationSessionAssessment : TenantOwnedEntity
{
    public long ExaminationSessionId { get; set; }
    public ExaminationSession Session { get; set; } = null!;
    public long AssessmentId { get; set; } // links to Assessments table (examination type assessment)
    public long SubjectId { get; set; }
    public long GradeId { get; set; }
    public long? StreamId { get; set; } // null = all streams in grade
}

public class ExaminationSlot : TenantOwnedEntity
{
    public long ExaminationSessionId { get; set; }
    public ExaminationSession Session { get; set; } = null!;
    public long SubjectId { get; set; }
    public long GradeId { get; set; }
    public long? StreamId { get; set; }
    public long AssessmentId { get; set; } // the examination assessment

    public DateTime ExamDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public long? VenueId { get; set; } // Room
    public string? VenueName { get; set; } // snapshot
    public long? InvigilatorStaffId { get; set; }
    public string? InvigilatorName { get; set; }

    public string Status { get; set; } = "scheduled"; // scheduled, completed, cancelled
}

// Weighted composite results: continuous assessment against examination, weights configurable per subject and per level
public class CompositeWeighting : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long? GradeId { get; set; } // null = all grades, or level (Form 1-4)
    public long? SubjectId { get; set; } // null = all subjects
    public decimal ContinuousAssessmentWeight { get; set; } // e.g. 30
    public decimal ExaminationWeight { get; set; } // e.g. 70
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

// Merit lists and rankings by class, stream, year group and subject, with configurable tie rule
public class MeritListConfig : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public string Scope { get; set; } = "class"; // class, stream, year_group, subject
    public string RankingMethod { get; set; } = "1224"; // 1224 standard competition, 1223 dense, 1224
    public string PositionBy { get; set; } = "average"; // average or aggregate
    public int TopN { get; set; } = 10; // top 10
    public bool IncludeTies { get; set; } = true;
}

// Promotion: rules based on aggregate, subject minimums and attendance
public class PromotionRule : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long FromGradeId { get; set; }
    public long ToGradeId { get; set; }
    public decimal MinimumAggregate { get; set; } // e.g. 50%
    public decimal? MinimumAverage { get; set; }
    public decimal? MinimumAttendancePercentage { get; set; } // e.g. 75%
    public int? MaxFailedSubjects { get; set; } // e.g. max 2 fails allowed
    public string? RequiredSubjectsJson { get; set; } // JSON list of subject_ids that must be passed
    public decimal? MinimumSubjectScore { get; set; } // minimum per subject e.g. 40%
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PromotionDecision : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long FromGradeId { get; set; }
    public long ToGradeId { get; set; }
    public long FromAcademicYearId { get; set; }
    public long ToAcademicYearId { get; set; }
    public long FromTermId { get; set; }

    public string RecommendedAction { get; set; } = "promoted"; // promoted, repeat, conditional, graduated
    public string FinalAction { get; set; } = "promoted"; // after manual override
    public bool IsManualOverride { get; set; } = false;
    public string? OverrideJustification { get; set; } // required if manual override
    public long? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }

    public string Reason { get; set; } = null!; // e.g. "Aggregate 45% < minimum 50%, failed Math 30% < 40%, attendance 80% >=75%"

    public decimal AggregateScore { get; set; }
    public decimal AverageScore { get; set; }
    public int FailedSubjectsCount { get; set; }
    public decimal AttendancePercentage { get; set; }

    public string Status { get; set; } = "pending"; // pending, approved, completed
    public long? NextEnrolmentId { get; set; } // created by bulk promotion job
}

public class PromotionBatch : TenantOwnedEntity
{
    public string BatchNumber { get; set; } = null!;
    public long FromAcademicYearId { get; set; }
    public long ToAcademicYearId { get; set; }
    public long FromGradeId { get; set; }
    public long ToGradeId { get; set; }
    public string Status { get; set; } = "pending"; // pending, running, completed, failed
    public int TotalStudents { get; set; }
    public int PromotedCount { get; set; }
    public int RepeatCount { get; set; }
    public int ConditionalCount { get; set; }
    public int FailedCount { get; set; }
    public string? ResultJson { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// Transcripts covering learner's full academic history across years
public class Transcript : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public string TranscriptNumber { get; set; } = null!; // TR-2026-00001
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public long GeneratedByUserId { get; set; }
    public string? DataJson { get; set; } // JSON of full history
    public string? PdfUrl { get; set; }
}

// Historical analysis: subject performance trends, class comparisons, learners whose performance dropped
public class HistoricalAnalysisCache : TenantOwnedEntity
{
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public long? GradeId { get; set; }
    public long? SubjectId { get; set; }
    public string AnalysisType { get; set; } = null!; // subject_trend, class_comparison, performance_drop
    public string DataJson { get; set; } = null!;
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

// Mark moderation: approval chain teacher -> HOD -> head, locked state after approval and audited reason for any change afterwards
public class MarkModeration : TenantOwnedEntity
{
    public long AssessmentId { get; set; }
    public long StudentId { get; set; }
    public long? SubjectId { get; set; }
    public string CurrentStage { get; set; } = "teacher"; // teacher, hod, head
    public string Status { get; set; } = "draft"; // draft, submitted_by_teacher, approved_by_hod, approved_by_head, locked
    public bool IsLocked { get; set; } = false;
    public DateTime? LockedAt { get; set; }
    public long? LockedByUserId { get; set; }

    public long? TeacherUserId { get; set; }
    public DateTime? TeacherSubmittedAt { get; set; }
    public long? HodUserId { get; set; }
    public DateTime? HodApprovedAt { get; set; }
    public long? HeadUserId { get; set; }
    public DateTime? HeadApprovedAt { get; set; }

    public string? ChangeReason { get; set; } // audited reason for any change after locked
    public string? PreviousScoreJson { get; set; }
    public string? NewScoreJson { get; set; }
}

// Certificates and printable award lists
public class Certificate : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public string CertificateType { get; set; } = null!; // merit, distinction, attendance, improvement
    public string Title { get; set; } = null!; // e.g. "Top 3 in Form 1A"
    public string? Description { get; set; }
    public string? DataJson { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public long IssuedByUserId { get; set; }
}
