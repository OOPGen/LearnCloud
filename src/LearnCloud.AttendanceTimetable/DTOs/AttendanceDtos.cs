namespace LearnCloud.AttendanceTimetable.DTOs;

// Tenant settings
public record TenantAttendanceSettingsDto(
    string Mode, // daily, per_period
    int BackdatingWindowDays,
    bool AllowBackdatingBeyondWindow,
    decimal ChronicAbsenceThreshold,
    bool CountLateAsPresent,
    bool CountExcusedAsPresent,
    bool CountSickAsPresent
);

public record UpdateAttendanceSettingsRequest(
    string Mode,
    int BackdatingWindowDays,
    decimal ChronicAbsenceThreshold,
    bool CountLateAsPresent,
    bool CountExcusedAsPresent,
    bool CountSickAsPresent
);

// Period definition
public record PeriodDefinitionDto(int PeriodNumber, string Name, string StartTime, string EndTime, bool IsBreak, int SortOrder);
public record CreatePeriodRequest(string Name, string StartTime, string EndTime, bool IsBreak, int SortOrder, long? AcademicYearId);
public record BulkPeriodsRequest(List<CreatePeriodRequest> Periods);

// Attendance register capture - optimized for speed
public record StudentForRegisterDto(long StudentId, string StudentNumber, string FirstName, string LastName, string? PhotoUrl, string CurrentStatus);
public record AttendanceRecordDto(long Id, long StudentId, string Status, string? AbsenceReason, string? Note, bool IsBackdated, DateTime MarkedAt);
public record RegisterHeaderDto(long Id, long GradeId, long StreamId, string GradeName, string StreamName, DateTime AttendanceDate, int? PeriodNumber, string PeriodName, int TotalStudents, int MarkedCount, bool IsBackdated, string Status);

public record GetRegisterRequest(long GradeId, long StreamId, DateTime AttendanceDate, int? PeriodNumber, long AcademicYearId, long TermId);
public record MarkAttendanceItemDto(long StudentId, string Status, string? AbsenceReason, string? Note); // Status: present, absent, late, sick, excused - cycle
public record MarkRegisterRequest(long GradeId, long StreamId, DateTime AttendanceDate, int? PeriodNumber, long AcademicYearId, long TermId, List<MarkAttendanceItemDto> Items, string? BackdateReason);
public record MarkRegisterResponse(long RegisterId, int MarkedCount, int TotalStudents, bool IsBackdated, DateTime SavedAt, string Message);

public record RegisterResponseDto(RegisterHeaderDto Header, List<StudentForRegisterDto> Students, List<AttendanceRecordDto> Records);

// Summaries
public record AttendanceSummaryPerLearnerDto(
    long StudentId,
    string StudentName,
    string StudentNumber,
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    int TotalDays,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    int SickCount,
    decimal Percentage,
    bool IsChronicAbsence,
    string ChronicMessage // plain language if flagged
);

public record AttendanceSummaryPerClassDto(
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    long AcademicYearId,
    long TermId,
    string TermName,
    int TotalStudents,
    decimal AveragePercentage,
    int ChronicAbsenceCount,
    List<AttendanceSummaryPerLearnerDto> Learners
);

public record AttendanceSummaryRequest(long? GradeId, long? StreamId, long AcademicYearId, long TermId, DateTime? FromDate, DateTime? ToDate);

// Printable A4 monthly register
public record PrintableMonthRegisterRequest(long GradeId, long StreamId, int Year, int Month, long AcademicYearId, long TermId);
public record PrintableMonthRegisterDto(
    string GradeName,
    string StreamName,
    int Year,
    int Month,
    string MonthName,
    List<DateTime> Dates, // days in month
    List<PrintableStudentRowDto> Rows
);
public record PrintableStudentRowDto(long StudentId, string StudentName, string StudentNumber, Dictionary<string,string> AttendanceByDate); // date string -> status letter P/A/L/E
