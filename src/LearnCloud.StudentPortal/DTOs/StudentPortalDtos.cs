namespace LearnCloud.StudentPortal.DTOs;

public record StudentDashboardDto(
    long StudentId,
    string StudentName,
    string StudentNumber,
    string GradeName,
    string StreamName,
    List<TimetableDayDto> TimetableWeek,
    AttendanceSummaryDto Attendance,
    LatestResultDto? LatestResult,
    List<AssignmentDto> AssignmentsDue,
    List<NoticeDto> RecentNotices,
    FeeSummaryDto? FeeSummary, // null if not allowed by school setting
    bool CanViewFees
);

public record TimetableDayDto(int DayOfWeek, string DayName, List<TimetableSlotDto> Slots);
public record TimetableSlotDto(int PeriodNumber, string PeriodName, string StartTime, string EndTime, string SubjectName, string TeacherName, string? RoomName, bool IsBreak);

public record AttendanceSummaryDto(int TotalDays, int Present, int Absent, int Late, int Excused, decimal Percentage, bool IsChronic);
public record AttendanceRecordDto(DateTime Date, string Status, string? Reason, string? Note, int? PeriodNumber, string PeriodName);

public record LatestResultDto(long ReportCardId, string TermName, decimal Average, string? Grade, DateTime PublishedAt);
public record AssignmentDto(long Id, string Title, string SubjectName, DateTime DueDate, int DaysLeft, string Status, bool CanSubmit, string? FileUrl);
public record NoticeDto(long Id, string Title, string Body, DateTime CreatedAt, string Priority);

public record FeeSummaryDto(decimal BalanceDue, string Currency, decimal TotalInvoiced, decimal TotalPaid, bool CanViewDetails);
public record FeeStatementLineDto(DateTime Date, string Type, string Number, decimal Debit, decimal Credit, decimal Balance, string Currency);

public record ReportCardDto(long Id, string TermName, decimal Average, string? OverallGrade, int? Position, DateTime PublishedAt, string? PdfUrl, string Status);
public record ReportCardDetailDto(ReportCardDto Header, List<SubjectResultDto> Subjects);
public record SubjectResultDto(string SubjectName, decimal? Score, decimal MaxScore, string? Grade, string? TeacherComment);

public record StudentProfileDto(long StudentId, long UserId, string StudentNumber, string FirstName, string LastName, string FullName, string? Email, string? Phone, string GradeName, string StreamName, string? PhotoUrl, DateTime? Dob, string Status);

public record UpdateStudentProfileRequest(string? Phone, string? Email);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

public record SubmitAssignmentRequest(string? Note, string? FileUrl, string? FileName);
