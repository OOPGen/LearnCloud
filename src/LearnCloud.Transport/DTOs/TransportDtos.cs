namespace LearnCloud.Transport.DTOs;

public record RouteDto(long Id, string Name, string Code, string? Description, string Direction, bool IsActive, long? AcademicYearId, long? TermId, decimal TotalDistanceKm, int EstimatedDurationMinutes, decimal FeeAmount, string Currency, long? VehicleId, string? VehicleReg, long? DriverId, string? DriverName, long? AssistantId, string? AssistantName, int StopsCount, int AssignedLearners, int Capacity, decimal UtilisationPercent);
public record CreateRouteRequest(string Name, string Code, string? Description, string Direction, long? AcademicYearId, long? TermId, decimal TotalDistanceKm, int EstimatedDurationMinutes, decimal FeeAmount, string Currency, long? VehicleId, long? DriverId, long? AssistantId);

public record RouteStopDto(long Id, long RouteId, string Name, string? Address, decimal? Latitude, decimal? Longitude, int OrderNumber, string? ExpectedArrivalTime, string? ExpectedDepartureTime, decimal DistanceFromStartKm, int EstimatedMinutesFromStart, bool IsActive);
public record CreateRouteStopRequest(string Name, string? Address, decimal? Latitude, decimal? Longitude, int OrderNumber, string? ExpectedArrivalTime, string? ExpectedDepartureTime, decimal DistanceFromStartKm, int EstimatedMinutesFromStart);
public record ReorderStopsRequest(List<long> OrderedStopIds);

public record VehicleDto(long Id, string RegistrationNumber, string Make, string Model, int Capacity, int Year, string? FuelType, DateTime InsuranceExpiry, DateTime LicenceExpiry, DateTime? FitnessExpiry, DateTime? ServiceDueDate, bool IsActive, string Status, int DaysToInsuranceExpiry, int DaysToLicenceExpiry, bool NeedsInsuranceReminder, bool NeedsLicenceReminder);
public record CreateVehicleRequest(string RegistrationNumber, string Make, string Model, int Capacity, int Year, string? FuelType, DateTime InsuranceExpiry, DateTime LicenceExpiry, DateTime? FitnessExpiry, DateTime? ServiceDueDate, string? Notes);

public record DriverDto(long Id, string FullName, string Role, long? StaffId, string? LicenceNumber, string? LicenceType, DateTime? LicenceExpiry, DateTime? MedicalExpiry, string? Phone, string? Email, bool IsActive, int DaysToLicenceExpiry, bool NeedsLicenceReminder);
public record CreateDriverRequest(string FullName, string Role, long? StaffId, string? LicenceNumber, string? LicenceType, DateTime? LicenceExpiry, DateTime? MedicalExpiry, string? Phone, string? Email, string? IdNumber, string? Notes);

public record TransportAssignmentDto(long Id, long StudentId, string StudentName, string StudentNumber, string GradeName, string StreamName, long RouteId, string RouteName, long PickupStopId, string PickupStopName, long? DropStopId, string? DropStopName, DateTime AssignedDate, string Status, long AcademicYearId, long TermId, decimal FeeAmount, string Currency, bool FeeApplied);
public record AssignLearnerRequest(long StudentId, long RouteId, long PickupStopId, long? DropStopId, long AcademicYearId, long TermId);
public record BulkAssignRequest(List<long> StudentIds, long RouteId, long PickupStopId, long? DropStopId, long AcademicYearId, long TermId);
public record UnassignLearnerRequest(string? Reason);

public record TransportAttendanceDto(long Id, long RouteId, long? RouteStopId, long StudentId, string StudentName, DateTime TripDate, string TripType, string Status, string? ActualBoardingTime, string? Notes);
public record MarkBoardingRequest(long RouteId, long? RouteStopId, DateTime TripDate, string TripType, List<BoardingItemDto> Items);
public record BoardingItemDto(long StudentId, string Status, string? Notes, string? ActualBoardingTime);

public record UtilisationReportDto(long RouteId, string RouteName, int Capacity, int Assigned, decimal UtilisationPercent, int BoardedToday, decimal BoardingRate, int StopsCount);
public record RevenueReportDto(long RouteId, string RouteName, decimal FeePerLearner, int AssignedLearners, decimal TotalInvoiced, decimal TotalCollected, decimal TotalArrears, decimal CollectionRate, string Currency);
public record UnassignedLearnersDto(List<UnassignedStudentDto> Students, int TotalUnassigned);
public record UnassignedStudentDto(long StudentId, string StudentName, string StudentNumber, string GradeName, string StreamName, string? Reason);

public record RouteManifestDto(long RouteId, string RouteName, string Code, string Direction, VehicleDto? Vehicle, DriverDto? Driver, DriverDto? Assistant, List<StopManifestDto> Stops, DateTime GeneratedAt);
public record StopManifestDto(long StopId, string StopName, string? Address, int OrderNumber, string? ExpectedArrivalTime, int LearnersCount, List<LearnerManifestDto> Learners);
public record LearnerManifestDto(long StudentId, string StudentName, string StudentNumber, string GradeName, string StreamName, string? GuardianPhone, string? GuardianName, string PickupStopName, string? DropStopName);
