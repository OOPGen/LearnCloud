namespace LearnCloud.Core.DTOs;

// ---- Academic calendar -----------------------------------------------------------------
// Dates are calendar dates: any time of day sent by a client is dropped.

public record CalendarYearDto(long Id, string Name, DateTime StartDate, DateTime EndDate, bool IsCurrent, List<CalendarTermDto> Terms);
public record CalendarTermDto(long Id, long AcademicYearId, string Name, int TermNumber, DateTime StartDate, DateTime EndDate, bool IsCurrent);
public record CurrentCalendarDto(CalendarYearDto? Year, CalendarTermDto? Term);

public record CreateAcademicYearRequest(string Name, DateTime StartDate, DateTime EndDate, bool IsCurrent);
public record UpdateAcademicYearRequest(string Name, DateTime StartDate, DateTime EndDate);
public record CreateTermRequest(string Name, int TermNumber, DateTime StartDate, DateTime EndDate, bool IsCurrent);
public record UpdateTermRequest(string Name, int TermNumber, DateTime StartDate, DateTime EndDate);

// ---- Grades and streams ----------------------------------------------------------------

public record GradeDto(long Id, long AcademicYearId, string Name, string Code, int LevelOrder, bool IsActive, List<StreamDto> Streams);
public record StreamDto(long Id, long GradeId, string Name, string DisplayName, int Capacity, int EnrolledCount);

public record CreateGradeRequest(long AcademicYearId, string Name, string Code, int LevelOrder);
public record UpdateGradeRequest(string Name, string Code, int LevelOrder, bool IsActive);
public record CreateStreamRequest(string Name, int Capacity);
public record UpdateStreamRequest(string Name, int Capacity);

// ---- Students and enrolments -----------------------------------------------------------

public record StudentListRequest(string? Search, string? Status, long? AcademicYearId, long? GradeId, long? StreamId, string? SortBy, bool SortDesc, int Page = 1, int PageSize = 25);

public record StudentListItemDto(
    long Id, string StudentNumber, string FirstName, string LastName, string? Gender, DateTime? Dob, string Status,
    long AcademicYearId, string AcademicYearName, long GradeId, string GradeName, long StreamId, string StreamName,
    bool HasCurrentEnrolment, string? PrimaryGuardianName, string? PrimaryGuardianPhone, DateTime CreatedAt);

public record StudentDetailDto(
    long Id, string StudentNumber, string FirstName, string LastName, DateTime? Dob, string? Gender, string? NationalId,
    string Status, PlacementDto? CurrentPlacement, List<EnrolmentDto> Enrolments, List<StudentGuardianDto> Guardians,
    DateTime CreatedAt, DateTime UpdatedAt);

public record PlacementDto(
    long EnrolmentId, long AcademicYearId, string AcademicYearName, long TermId, string TermName,
    long GradeId, string GradeName, long StreamId, string StreamName, DateTime EnrolmentDate);

public record EnrolmentDto(
    long Id, long AcademicYearId, string AcademicYearName, long TermId, string TermName, long GradeId, string GradeName,
    long StreamId, string StreamName, string EnrolmentStatus, string EnrolmentType, DateTime EnrolmentDate, DateTime? ExitDate, bool IsCurrent);

/// <summary>A new student and their first class. StudentNumber is generated when blank.</summary>
public record CreateStudentRequest(
    string FirstName, string LastName, DateTime? Dob, string? Gender, string? NationalId, string? StudentNumber,
    long StreamId, long? TermId, DateTime? EnrolmentDate, string? EnrolmentType, LinkGuardianRequest? Guardian);

public record UpdateStudentRequest(string FirstName, string LastName, DateTime? Dob, string? Gender, string? NationalId, string StudentNumber);

/// <summary>Moves a student to another class, promotes them into a later year, or readmits them.</summary>
public record ChangeEnrolmentRequest(long StreamId, long? TermId, DateTime? EffectiveDate);

/// <summary>Ends the current enrolment. Reason: withdrawn, transferred_out or graduated.</summary>
public record ExitStudentRequest(string Reason, DateTime? ExitDate);

// ---- Guardians -------------------------------------------------------------------------

public record GuardianInput(string FirstName, string LastName, string Phone, string? Email, string? Address, string? NationalId);

/// <summary>Links an existing guardian (GuardianId) or a new one (NewGuardian) to a student.</summary>
public record LinkGuardianRequest(
    long? GuardianId, GuardianInput? NewGuardian, string RelationshipType,
    bool IsPrimaryContact, bool IsBillingContact, bool IsEmergencyContact, bool CanPickup);

public record UpdateGuardianLinkRequest(string RelationshipType, bool IsPrimaryContact, bool IsBillingContact, bool IsEmergencyContact, bool CanPickup);

public record StudentGuardianDto(
    long LinkId, long GuardianId, string FirstName, string LastName, string Phone, string? Email, string RelationshipType,
    bool IsPrimaryContact, bool IsBillingContact, bool IsEmergencyContact, bool CanPickup);

public record GuardianListRequest(string? Search, int Page = 1, int PageSize = 25);
public record GuardianListItemDto(long Id, string FirstName, string LastName, string Phone, string? Email, int StudentCount, DateTime CreatedAt);

public record GuardianDetailDto(
    long Id, string FirstName, string LastName, string Phone, string? Email, string? Address, string? NationalId,
    List<GuardianStudentDto> Students, DateTime CreatedAt);

public record GuardianStudentDto(
    long LinkId, long StudentId, string StudentNumber, string FirstName, string LastName, string Status, string ClassName,
    string RelationshipType, bool IsPrimaryContact, bool IsBillingContact, bool IsEmergencyContact, bool CanPickup);
