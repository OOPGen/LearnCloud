namespace LearnCloud.Examinations.DTOs;

public record CreateExaminationSessionRequest(string Name, long AcademicYearId, long TermId, string SessionType, DateTime StartDate, DateTime EndDate, string? Description);
public record ExaminationSessionDto(long Id, string Name, long AcademicYearId, long TermId, string SessionType, string Status, DateTime StartDate, DateTime EndDate, int SlotsCount);

public record CreateExaminationSlotRequest(long SubjectId, long GradeId, long? StreamId, long AssessmentId, DateTime ExamDate, string StartTime, string EndTime, long? VenueId, long? InvigilatorStaffId);
public record ExaminationSlotDto(long Id, long SessionId, long SubjectId, string SubjectName, long GradeId, string GradeName, long? StreamId, string? StreamName, long AssessmentId, string AssessmentName, DateTime ExamDate, string StartTime, string EndTime, long? VenueId, string? VenueName, long? InvigilatorStaffId, string? InvigilatorName, string Status);

public record CompositeWeightingDto(long Id, long AcademicYearId, long TermId, long? GradeId, long? SubjectId, decimal CAWeight, decimal ExamWeight, string? Description);
public record CreateCompositeWeightingRequest(long AcademicYearId, long TermId, long? GradeId, long? SubjectId, decimal CAWeight, decimal ExamWeight, string? Description);

public record MeritListRequest(long AcademicYearId, long TermId, string Scope, long? GradeId, long? StreamId, long? SubjectId, string PositionBy, string TieRule, int TopN);
public record MeritListEntryDto(long StudentId, string StudentName, string StudentNumber, string GradeName, string StreamName, decimal Aggregate, decimal Average, int? Rank, bool IsTie, string? SubjectName);

public record PromotionRuleDto(long Id, long AcademicYearId, long FromGradeId, long ToGradeId, decimal MinimumAggregate, decimal? MinimumAverage, decimal? MinimumAttendance, int? MaxFailedSubjects, decimal? MinimumSubjectScore, string? Description);
public record CreatePromotionRuleRequest(long AcademicYearId, long FromGradeId, long ToGradeId, decimal MinimumAggregate, decimal? MinimumAverage, decimal? MinimumAttendance, int? MaxFailedSubjects, decimal? MinimumSubjectScore, string? Description, string? RequiredSubjectsJson);

public record PromotionDecisionDto(long Id, long StudentId, string StudentName, long FromGradeId, long ToGradeId, string RecommendedAction, string FinalAction, bool IsManualOverride, string? OverrideJustification, string Reason, decimal Aggregate, decimal Average, int FailedCount, decimal Attendance, string Status);
public record EvaluatePromotionRequest(List<long> StudentIds, long FromAcademicYearId, long ToAcademicYearId, long FromGradeId, long ToGradeId, long FromTermId);
public record OverridePromotionRequest(long DecisionId, string FinalAction, string Justification);

public record TranscriptRequest(long StudentId);
public record TranscriptDto(long StudentId, string StudentName, List<TermTranscriptDto> Terms, List<SubjectTrendDto> SubjectTrends, string? PdfUrl, string TranscriptNumber);
public record TermTranscriptDto(long AcademicYearId, long TermId, string TermName, decimal Aggregate, decimal Average, int? Rank, List<SubjectResultDto> Subjects);
public record SubjectResultDto(long SubjectId, string SubjectName, decimal? Score, string? Grade);
public record SubjectTrendDto(long SubjectId, string SubjectName, List<decimal> ScoresOverTerms, string Trend); // up, down, stable

public record HistoricalAnalysisRequest(long AcademicYearId, long TermId, string AnalysisType, long? GradeId, long? SubjectId);
public record PerformanceDropDto(long StudentId, string StudentName, long PreviousYear, long PreviousTerm, decimal PreviousAverage, long CurrentYear, long CurrentTerm, decimal CurrentAverage, decimal Drop, string Reason);

public record MarkModerationDto(long Id, long AssessmentId, long StudentId, string CurrentStage, string Status, bool IsLocked, DateTime? LockedAt, long? TeacherUserId, DateTime? TeacherSubmittedAt, string? ChangeReason);
public record SubmitForModerationRequest(long AssessmentId, List<long> StudentIds);
public record ModerateMarksRequest(long AssessmentId, string Action, string? Comment, string? ChangeReason); // approve, reject, unlock

public record CertificateDto(long Id, long StudentId, string StudentName, string CertificateType, string Title, string? Description, DateTime IssuedAt, string? PdfUrl);
public record CreateCertificateRequest(long StudentId, long AcademicYearId, long TermId, string CertificateType, string Title, string? Description);
