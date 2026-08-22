namespace LearnCloud.AI.DTOs;

// 1. Report card comment drafting
public record GenerateCommentRequest(
    long StudentId,
    long AcademicYearId,
    long TermId,
    long? ReportCardId,
    string Tone, // encouraging, formal, concise, detailed, neutral
    string Length, // short, medium, long
    bool IncludeAttendance = true,
    bool IncludeSubjectDetails = true,
    string? CustomInstructions = null // teacher can add "Focus on effort"
);

public record GenerateCommentResponse(
    long DraftId,
    long StudentId,
    string StudentName,
    string DraftComment,
    string Tone,
    string Length,
    List<SubjectPerformanceDto> SubjectPerformances,
    AttendanceForCommentDto Attendance,
    string ProviderName,
    string? Model,
    bool RequiresReview // always true - teacher must review
);

public record SubjectPerformanceDto(long SubjectId, string SubjectName, decimal? Score, decimal MaxScore, string? Grade, decimal? ClassAverage, string Trend, string StrengthWeakness);
public record AttendanceForCommentDto(int TotalDays, int Present, int Absent, int Late, decimal Percentage, string Summary);

public record ReviewCommentRequest(long DraftId, string EditedComment);
public record SaveCommentRequest(long DraftId, string FinalComment);

// 2. Attendance anomaly detection
public record AttendanceAnomalyDto(long Id, long StudentId, string StudentName, string StudentNumber, string AnomalyType, string Description, string Explanation, decimal ConfidenceScore, DateTime DetectedAt, DateTime PeriodFrom, DateTime PeriodTo, string Status, string? DataJson);
public record DetectAnomaliesRequest(long? GradeId, long? StreamId, long? AcademicYearId, long? TermId, int? DaysBack, decimal? Threshold);

// 3. At-risk learner identification
public record AtRiskFlagDto(long Id, long StudentId, string StudentName, string StudentNumber, string GradeName, string StreamName, string RiskLevel, decimal RiskScore, string FlagReason, List<UnderlyingReasonDto> UnderlyingReasons, DateTime DetectedAt, string Status, long? AssignedToUserId, string? AssignedToName);
public record UnderlyingReasonDto(string Type, string Detail, string Severity, decimal? Score, string? Trend);
public record DetectAtRiskRequest(long? GradeId, long? StreamId, long? AcademicYearId, long? TermId, decimal? MarksDropThreshold, decimal? AttendanceDropThreshold, decimal? ArrearsThreshold);

public record AIProviderSettingsDto(long Id, string ProviderName, bool IsActive, bool IsDefault, bool EnableCommentDrafting, bool EnableAttendanceAnomaly, bool EnableAtRiskDetection);
public record UpdateAIProviderRequest(string ProviderName, bool IsActive, bool EnableCommentDrafting, bool EnableAttendanceAnomaly, bool EnableAtRiskDetection, string? ConfigJson);
