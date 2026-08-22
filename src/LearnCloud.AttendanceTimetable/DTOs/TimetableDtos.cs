namespace LearnCloud.AttendanceTimetable.DTOs;

public record TimetableDto(long Id, string Name, long AcademicYearId, long TermId, DateTime EffectiveFrom, DateTime? EffectiveTo, int Version, string Status, int SlotsCount);
public record CreateTimetableRequest(string Name, long AcademicYearId, long TermId, DateTime EffectiveFrom, DateTime? EffectiveTo, long? CloneFromTimetableId);

public record TimetableSlotDto(
    long Id,
    long TimetableId,
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    long SubjectId,
    string SubjectName,
    long TeacherStaffId,
    string TeacherName,
    long? RoomId,
    string? RoomName,
    int DayOfWeek,
    string DayName,
    int PeriodNumber,
    string PeriodName,
    string StartTime,
    string EndTime
);

public record CreateSlotRequest(
    long GradeId,
    long StreamId,
    long SubjectId,
    long TeacherStaffId,
    long? RoomId,
    int DayOfWeek,
    int PeriodNumber
);

public record UpdateSlotRequest(
    long GradeId,
    long StreamId,
    long SubjectId,
    long TeacherStaffId,
    long? RoomId,
    int DayOfWeek,
    int PeriodNumber
);

public record BulkSlotsRequest(List<CreateSlotRequest> Slots);

public record ClashResponseDto(
    bool HasClash,
    List<ClashDetailDto> Clashes
);

public record ClashDetailDto(
    string ClashType, // Teacher, Class, Room
    string Message, // plain language: Mrs Moyo already teaching Form 2B Math at Period 2 Monday in Room 5
    long ExistingSlotId,
    string ExistingInfo,
    long NewSlotId,
    string NewInfo,
    int DayOfWeek,
    int PeriodNumber,
    long TeacherStaffId,
    string TeacherName,
    long GradeId,
    string GradeName,
    long StreamId,
    string StreamName,
    long? RoomId,
    string? RoomName
);

public record TimetableGridDto(
    long TimetableId,
    string TimetableName,
    List<PeriodDefinitionDto> Periods,
    List<DayColumnDto> Days
);

public record DayColumnDto(int DayOfWeek, string DayName, List<TimetableSlotDto> Slots);

public record TimetableViewRequest(long? GradeId, long? StreamId, long? TeacherStaffId, long AcademicYearId, long TermId, DateTime? EffectiveDate, long? TimetableId);

public record PeriodDefinitionDto(int PeriodNumber, string Name, string StartTime, string EndTime, bool IsBreak, int SortOrder);
