namespace LearnCloud.Hostel.DTOs;

public record BlockDto(long Id, string Name, string Code, string GenderDesignation, int Capacity, int TotalRooms, int OccupiedBeds, int AvailableBeds, decimal OccupancyPercent, bool IsActive, long? HouseMasterStaffId, string? HouseMasterName, long? MatronStaffId, string? MatronName);
public record CreateBlockRequest(string Name, string Code, string GenderDesignation, int Capacity, int TotalRooms, string? Description, string? Location, long? HouseMasterStaffId, long? MatronStaffId);

public record RoomDto(long Id, long BlockId, string BlockName, string RoomNumber, int Floor, int Capacity, string GenderDesignation, bool IsActive, int OccupiedBeds, int AvailableBeds);
public record CreateRoomRequest(long BlockId, string RoomNumber, int Floor, int Capacity, string GenderDesignation, string? Facilities);

public record BedDto(long Id, long RoomId, long BlockId, string BedNumber, string RoomNumber, string BlockName, string Status, string Condition, long? CurrentStudentId, string? CurrentStudentName);
public record CreateBedRequest(long RoomId, string BedNumber, string Condition);

public record AllocationDto(long Id, long StudentId, string StudentName, string StudentNumber, long BedId, string BedNumber, long RoomId, string RoomNumber, long BlockId, string BlockName, long AcademicYearId, long TermId, DateTime AllocationDate, string Status, bool FeeApplied, decimal? FeeAmount);
public record AllocateRequest(long StudentId, long BedId, long AcademicYearId, long TermId);
public record TransferBedRequest(long NewBedId, string? Reason);
public record WaitingListDto(long Id, long StudentId, string StudentName, long? PreferredBlockId, string? PreferredBlockName, string Gender, long AcademicYearId, long TermId, int Priority, int QueuePosition, string Status, string? Reason, DateTime RequestedDate);

public record ExeatDto(long Id, long StudentId, string StudentName, long BlockId, string BlockName, string LeaveType, string Reason, DateTime DepartureDateTime, DateTime ExpectedReturnDateTime, DateTime? ActualReturnDateTime, string Status, long AuthorisedByUserId, string AuthoriserRole, string? ContactPhone, bool IsOverdue);
public record CreateExeatRequest(long StudentId, long BlockId, string LeaveType, string Reason, DateTime DepartureDateTime, DateTime ExpectedReturnDateTime, string? ContactPhone, string? DestinationAddress, string? AccompanyingPerson);
public record ReturnFromLeaveRequest(DateTime ActualReturnDateTime, string? Notes);

public record RollCallDto(long Id, long BlockId, string BlockName, long? RoomId, string? RoomNumber, DateTime RollCallDate, string RollCallType, string Status, int TotalExpected, int TotalPresent, int TotalAbsent, int TotalOnLeave, long ConductedByUserId);
public record CreateRollCallRequest(long BlockId, long? RoomId, DateTime RollCallDate, string RollCallType);
public record RollCallEntryDto(long Id, long RollCallId, long BedId, string BedNumber, long StudentId, string StudentName, DateTime RollCallDate, string Status, string? Notes);
public record MarkRollCallRequest(List<RollCallMarkDto> Entries);
public record RollCallMarkDto(long BedId, long StudentId, string Status, string? Notes);

public record VisitorLogDto(long Id, long StudentId, string StudentName, long BlockId, string BlockName, string VisitorName, string Relationship, string IdNumber, string Phone, DateTime CheckInDateTime, DateTime? CheckOutDateTime, string Purpose, string Status, long AuthorisedByUserId);
public record CreateVisitorLogRequest(long StudentId, long BlockId, string VisitorName, string Relationship, string IdNumber, string Phone, string Purpose, string? Belongings);
public record CheckoutVisitorRequest(DateTime CheckOutDateTime);

public record IncidentDto(long Id, long BlockId, long? RoomId, long StudentId, string StudentName, string IncidentType, string Title, string Description, string Severity, DateTime IncidentDateTime, string Status, string Visibility, bool IsConfidential);
public record CreateIncidentRequest(long BlockId, long? RoomId, long StudentId, string IncidentType, string Title, string Description, string Severity, DateTime IncidentDateTime, string? ActionTaken, string Visibility, bool IsConfidential);

public record SickBayDto(long Id, long StudentId, string StudentName, long BlockId, DateTime CheckInDateTime, DateTime? CheckOutDateTime, string Symptoms, string? Diagnosis, string? Treatment, string Status, string Visibility);
public record CreateSickBayRequest(long StudentId, long BlockId, string Symptoms, string? Diagnosis, string? Treatment, string? Medication, string? Notes);

public record OccupancyReportDto(long BlockId, string BlockName, int Capacity, int Occupied, int Available, int Reserved, int Maintenance, decimal OccupancyPercent, List<RoomOccupancyDto> Rooms);
public record RoomOccupancyDto(long RoomId, string RoomNumber, int Capacity, int Occupied, int Available);
public record VacancyReportDto(List<BedDto> VacantBeds, int TotalVacant, Dictionary<string,int> ByBlock, Dictionary<string,int> ByGender);
public record OnLeaveReportDto(List<ExeatDto> OnLeave, List<ExeatDto> Overdue, int TotalOnLeave, int TotalOverdue);
public record BoardingRevenueDto(long BlockId, string BlockName, decimal FeePerLearner, int AllocatedLearners, decimal TotalInvoiced, decimal TotalCollected, decimal TotalArrears, decimal CollectionRate, string Currency);
