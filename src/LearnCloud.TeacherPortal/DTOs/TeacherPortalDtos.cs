namespace LearnCloud.TeacherPortal.DTOs;

// Dashboard: today's timetable, registers still to be marked, marks deadlines, unread notices
public record TodayTimetableItemDto(
    long SlotId,
    int DayOfWeek,
    int PeriodNumber,
    string PeriodName,
    string StartTime,
    string EndTime,
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    long SubjectId,
    string SubjectName,
    long? RoomId,
    string? RoomName
);

public record RegisterTodoDto(
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    DateTime Date,
    int? PeriodNumber,
    string PeriodName,
    bool IsOverdue,
    int StudentsCount
);

public record MarksDeadlineDto(
    long AssessmentId,
    string AssessmentName,
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    long SubjectId,
    string SubjectName,
    DateTime DueDate,
    int DaysLeft,
    int TotalStudents,
    int MarkedCount,
    int UnmarkedCount,
    string Status // draft, submitted, approved
);

public record NoticeDto(
    long Id,
    string Title,
    string Body,
    string Priority,
    bool IsRead,
    DateTime CreatedAt
);

public record TeacherDashboardDto(
    List<TodayTimetableItemDto> TodayTimetable,
    List<RegisterTodoDto> RegistersToMark,
    List<MarksDeadlineDto> MarksDeadlines,
    List<NoticeDto> UnreadNotices,
    int TotalClasses,
    int TotalLearners,
    string TeacherName
);

// My classes: only classes teacher is assigned to, enforced server-side
public record MyClassDto(
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    string FullName, // Grade 5 Blue
    int Capacity,
    int CurrentEnrolment,
    bool IsClassTeacher,
    List<string> Subjects, // subjects teacher teaches in this class
    int LearnersCount
);

// Attendance capture reuses existing register screen - DTOs already in AttendanceTimetable module
// Marks entry: keyboard navigable grid
public record MarksGridRowDto(
    long StudentId,
    string StudentNumber,
    string FirstName,
    string LastName,
    string? PhotoUrl,
    decimal? Score,
    bool IsAbsent,
    string? Comment,
    string Status // draft, saved
);

public record MarksGridDto(
    long AssessmentId,
    string AssessmentName,
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    long SubjectId,
    string SubjectName,
    decimal MaxScore,
    DateTime AssessmentDate,
    string CategoryName,
    decimal Weight,
    List<MarksGridRowDto> Rows,
    string OverallStatus, // draft, submitted, approved
    DateTime? SubmittedAt
);

public record SaveMarksItemDto(long StudentId, decimal? Score, bool IsAbsent, string? Comment);
public record SaveMarksRequest(List<SaveMarksItemDto> Items, bool SaveAsDraft = true);
public record SubmitMarksRequest(bool Confirm = true);

// Homework and assignments
public record HomeworkAssignmentDto(
    long Id,
    string Title,
    string Description,
    string? FileUrl,
    string? FileName,
    DateTime DueDate,
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    long SubjectId,
    string SubjectName,
    string Status,
    int TotalStudents,
    int SubmittedCount,
    int PendingCount,
    int LateCount,
    DateTime CreatedAt
);

public record CreateHomeworkRequest(
    string Title,
    string Description,
    long GradeId,
    long StreamId,
    long SubjectId,
    DateTime DueDate,
    string? FileUrl,
    string? FileName
);

public record HomeworkSubmissionDto(
    long Id,
    long AssignmentId,
    long StudentId,
    string StudentName,
    string StudentNumber,
    string Status,
    DateTime? SubmittedAt,
    string? FileUrl,
    string? Note
);

public record UpdateSubmissionFeedbackRequest(string? TeacherFeedback, decimal? Score);

// Lesson plans
public record LessonPlanDto(
    long Id,
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    long SubjectId,
    string SubjectName,
    DateTime Date,
    string Objective,
    string Activities,
    string Resources,
    string Assessment,
    string Reflection,
    string Status
);

public record CreateLessonPlanRequest(
    long GradeId,
    long StreamId,
    long SubjectId,
    DateTime Date,
    string Objective,
    string Activities,
    string Resources,
    string Assessment,
    string Reflection,
    string Status = "draft"
);

// Read-only view of learners in class with guardian contact details
public record LearnerInClassDto(
    long StudentId,
    string StudentNumber,
    string FirstName,
    string LastName,
    string? PhotoUrl,
    DateTime? Dob,
    string Gender,
    string Status,
    List<GuardianContactDto> Guardians,
    decimal? AttendancePercentage,
    decimal? AverageScore
);

public record GuardianContactDto(
    long GuardianId,
    string Name,
    string Relationship,
    string Phone,
    string? Email,
    bool IsPrimary,
    bool IsBilling,
    bool IsEmergency,
    bool CanPickup,
    bool SmsOptIn,
    bool EmailOptIn
);

// Profile and password management
public record TeacherProfileDto(
    long StaffId,
    long UserId,
    string StaffNumber,
    string FirstName,
    string LastName,
    string DisplayName,
    string Email,
    string? Phone,
    string EmploymentType,
    string Qualification,
    List<string> SubjectsTaught,
    List<MyClassDto> Classes,
    bool IsClassTeacher,
    DateTime? LastLoginAt
);

public record UpdateProfileRequest(
    string? Phone,
    string? Qualification
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);
