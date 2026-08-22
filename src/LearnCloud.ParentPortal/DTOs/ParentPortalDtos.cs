namespace LearnCloud.ParentPortal.DTOs;

// Login invitation flow
public record InviteGuardianRequest(long GuardianId, string Email, string? Phone, string? Message);
public record InvitationDto(long Id, long GuardianId, string Email, DateTime ExpiresAt, bool IsUsed, DateTime? UsedAt, string InvitationLink);
public record AcceptInvitationRequest(string Token, string Email, string NewPassword, string ConfirmPassword);

// Child switcher
public record ChildDto(long StudentId, string StudentNumber, string FirstName, string LastName, string FullName, string GradeName, string StreamName, long GradeId, long StreamId, string? PhotoUrl, string Status);
public record ChildrenListDto(List<ChildDto> Children);

// Home screen per child
public record ChildHomeDto(
    long StudentId,
    string StudentName,
    string GradeName,
    string StreamName,
    decimal OutstandingBalance,
    string Currency,
    decimal AttendancePercentage,
    bool IsChronicAbsence,
    LatestResultDto? LatestResult,
    List<UpcomingAssessmentDto> UpcomingAssessments,
    List<NoticeDto> RecentNotices,
    List<HomeworkDto> HomeworkDue
);

public record LatestResultDto(long ReportCardId, string TermName, decimal Average, string? OverallGrade, DateTime PublishedAt, string? PdfUrl);
public record UpcomingAssessmentDto(long AssessmentId, string AssessmentName, string SubjectName, DateTime AssessmentDate, int DaysLeft, decimal MaxScore);
public record NoticeDto(long Id, string Title, string Body, DateTime CreatedAt, string Priority, bool IsRead);
public record HomeworkDto(long Id, string Title, string SubjectName, DateTime DueDate, int DaysLeft, string Status);

// Fees
public record FeeStatementLineDto(DateTime Date, string Type, string Number, string Description, decimal Debit, decimal Credit, decimal Balance, string Currency);
public record FeeStatementDto(long StudentId, string StudentName, string StudentNumber, List<FeeStatementLineDto> Lines, decimal TotalInvoiced, decimal TotalPaid, decimal BalanceDue, decimal Credit, string Currency);
public record InvoiceHistoryDto(long InvoiceId, string InvoiceNumber, DateTime IssueDate, DateTime DueDate, decimal TotalAmount, decimal AmountPaid, decimal BalanceDue, string Status, string Currency);
public record ReceiptDto(long PaymentId, string ReceiptNumber, DateTime PaymentDate, decimal Amount, string Currency, string Method, string? Reference);

// Attendance detail
public record AttendanceDetailDto(DateTime Date, string Status, string? Reason, string? Note, int? PeriodNumber, string PeriodName);
public record AttendanceSummaryDto(int TotalDays, int Present, int Absent, int Late, int Excused, int Sick, decimal Percentage, bool IsChronic, string GradeName, string StreamName);

// Results published only
public record ReportCardDto(long Id, string TermName, long AcademicYearId, long TermId, decimal Average, string? OverallGrade, int? Position, string? PositionDisplay, DateTime PublishedAt, string? PdfUrl, string Status, string? ClassTeacherComment, string? HeadComment, DateTime? NextTermStartDate);
public record ReportCardDetailDto(ReportCardDto Header, List<SubjectResultDto> Subjects, AttendanceSummaryDto Attendance, List<string> GuardianComments);
public record SubjectResultDto(long SubjectId, string SubjectName, decimal? Score, decimal MaxScore, string? Grade, string? TeacherComment, decimal? ClassAverage);

// Notices and homework
public record AllNoticesDto(List<NoticeDto> Notices);
public record AllHomeworkDto(List<HomeworkDto> Homework);

// Message to class teacher
public record SendMessageToTeacherRequest(long StudentId, long GradeId, long StreamId, string Subject, string Body, long? RecipientTeacherStaffId);
public record TeacherMessageDto(long Id, long StudentId, string StudentName, string Subject, string Body, string Status, DateTime CreatedAt, string? ModerationNote, bool RequiresModeration);
public record MessageThreadDto(List<TeacherMessageDto> Messages);

// Profile and contact preferences including SMS opt-out
public record ParentProfileDto(long GuardianId, long UserId, string FirstName, string LastName, string FullName, string? Email, string Phone, string? Address, List<ChildDto> Children, ContactPreferencesDto ContactPreferences);
public record ContactPreferencesDto(bool SmsOptIn, bool EmailOptIn, bool SmsOptOut, bool EmailOptOut, string? PreferredLanguage, bool CanReceiveFeesSms, bool CanReceiveAttendanceSms, bool CanReceiveGeneralNotice);
public record UpdateContactPreferencesRequest(bool SmsOptIn, bool EmailOptIn, bool SmsOptOut, bool EmailOptOut, string? PreferredLanguage, bool CanReceiveFeesSms, bool CanReceiveAttendanceSms, bool CanReceiveGeneralNotice);
