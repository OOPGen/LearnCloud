namespace LearnCloud.HR.DTOs;

public record DepartmentDto(long Id, string Name, string Code, long? HodStaffId, string? HodName, bool IsActive);
public record CreateDepartmentRequest(string Name, string Code, long? HodStaffId);

public record StaffDto(long Id, string StaffNumber, string FirstName, string LastName, string FullName, string? NationalId, DateTime? DateOfBirth, string Gender, string EmploymentType, string EmploymentStatus, long? DepartmentId, string? DepartmentName, string? Designation, DateTime HireDate, string? Phone, string? Email, string? PhotoUrl, decimal? CurrentSalary, string Currency, long? UserId);
public record CreateStaffRequest(string FirstName, string LastName, string? NationalId, DateTime? DateOfBirth, string Gender, string EmploymentType, long? DepartmentId, string? Designation, DateTime HireDate, string? Phone, string? Email, decimal? CurrentSalary, string Currency, long? UserId);
public record UpdateStaffRequest(string? FirstName, string? LastName, string? Phone, string? Email, string? Address, string? Designation, long? DepartmentId, string EmploymentStatus);

public record ContractDto(long Id, long StaffId, string ContractNumber, string ContractType, DateTime StartDate, DateTime EndDate, DateTime? ProbationEndDate, decimal? Salary, string Currency, string Status, string? Terms, int DaysToExpiry, bool NeedsReminder);
public record CreateContractRequest(long StaffId, string ContractType, DateTime StartDate, DateTime EndDate, DateTime? ProbationEndDate, decimal? Salary, string Currency, string? Terms);

public record QualificationDto(long Id, long StaffId, string QualificationName, string Institution, int? YearObtained, string? Grade, string? CertificateNumber, bool IsVerified);
public record CreateQualificationRequest(long StaffId, string QualificationName, string Institution, int? YearObtained, string? Grade, string? CertificateNumber);

public record StaffDocumentDto(long Id, long StaffId, string DocumentType, string FileName, string FileUrl, long FileSize, string ContentType, DateTime? ExpiryDate, bool IsVerified, long UploadedByUserId);
public record UploadDocumentRequest(long StaffId, string DocumentType, string FileName, string FileUrl, long FileSize, string ContentType, DateTime? ExpiryDate);

public record LeaveTypeDto(long Id, string Name, string Code, string Description, int DefaultEntitlementDays, bool IsPaid, bool RequiresDocument, bool IsCarryForwardAllowed, int MaxCarryForwardDays, string AccrualRule, bool IsActive);
public record CreateLeaveTypeRequest(string Name, string Code, string Description, int DefaultEntitlementDays, bool IsPaid, bool RequiresDocument, bool IsCarryForwardAllowed, int MaxCarryForwardDays, string AccrualRule);

public record LeaveEntitlementDto(long Id, long StaffId, string StaffName, long LeaveTypeId, string LeaveTypeName, int AcademicYear, decimal EntitledDays, decimal CarriedForwardDays, decimal UsedDays, decimal RemainingDays, DateTime? ExpiryDate);
public record CreateEntitlementRequest(long StaffId, long LeaveTypeId, int AcademicYear, decimal EntitledDays, decimal CarriedForwardDays, DateTime? ExpiryDate);

public record LeaveRequestDto(long Id, long StaffId, string StaffName, long LeaveTypeId, string LeaveTypeName, DateTime StartDate, DateTime EndDate, decimal DaysRequested, string Reason, string Status, long? ApproverUserId, string? ApproverComment, string? DocumentUrl, bool IsHalfDay, DateTime CreatedAt);
public record CreateLeaveRequestDto(long StaffId, long LeaveTypeId, DateTime StartDate, DateTime EndDate, string Reason, bool IsHalfDay, string? DocumentUrl);
public record ApproveLeaveRequest(string Status, string? ApproverComment);

public record LeaveBalanceDto(long StaffId, string StaffName, List<LeaveEntitlementDto> Entitlements, decimal TotalEntitled, decimal TotalUsed, decimal TotalRemaining);
public record LeaveCalendarDto(DateTime Date, List<LeaveRequestDto> LeavesOnDate, int CountOnLeave);

public record AppraisalCycleDto(long Id, string Name, long AcademicYearId, long? TermId, DateTime StartDate, DateTime EndDate, string Status, string? Description, List<AppraisalCriterionDto> Criteria);
public record CreateAppraisalCycleRequest(string Name, long AcademicYearId, long? TermId, DateTime StartDate, DateTime EndDate, string? Description, List<CreateCriterionRequest> Criteria);
public record AppraisalCriterionDto(long Id, long AppraisalCycleId, string Name, string? Description, int Weight, int MaxScore, int SortOrder);
public record CreateCriterionRequest(string Name, string? Description, int Weight, int MaxScore, int SortOrder);

public record AppraisalDto(long Id, long AppraisalCycleId, string CycleName, long StaffId, string StaffName, long AppraiserUserId, string Status, decimal OverallScore, string? OverallComment, string? StaffComment, DateTime? SubmittedAt, List<AppraisalScoreDto> Scores);
public record CreateAppraisalRequest(long AppraisalCycleId, long StaffId, string? OverallComment, List<CreateScoreRequest> Scores);
public record AppraisalScoreDto(long Id, long CriterionId, string CriterionName, int Score, string? Comment);
public record CreateScoreRequest(long CriterionId, int Score, string? Comment);
public record AcknowledgeAppraisalRequest(string? StaffComment);

public record DisciplinaryRecordDto(long Id, long StaffId, string StaffName, DateTime IncidentDate, string IncidentType, string Title, string Description, string Severity, string ActionTaken, string Status, long ReportedByUserId, string Visibility, bool IsConfidential, DateTime? ResolutionDate);
public record CreateDisciplinaryRequest(long StaffId, DateTime IncidentDate, string IncidentType, string Title, string Description, string Severity, string ActionTaken, string Visibility, bool IsConfidential);

public record HeadcountReportDto(int TotalActive, int TotalOnLeave, int TotalTerminated, Dictionary<string,int> ByDepartment, Dictionary<string,int> ByEmploymentType, List<MonthlyHeadcountDto> MonthlyTrend);
public record MonthlyHeadcountDto(int Year, int Month, int Headcount, int Joined, int Left);
public record TurnoverReportDto(int Year, int TotalLeft, decimal TurnoverRate, List<DepartmentTurnoverDto> ByDepartment, List<MonthlyTurnoverDto> Monthly);
public record DepartmentTurnoverDto(long DepartmentId, string DepartmentName, int Left, decimal Rate);
public record MonthlyTurnoverDto(int Year, int Month, int Left, int Joined);
public record LeaveLiabilityReportDto(decimal TotalLiabilityDays, decimal TotalLiabilityAmount, string Currency, List<StaffLeaveLiabilityDto> ByStaff);
public record StaffLeaveLiabilityDto(long StaffId, string StaffName, decimal RemainingDays, decimal DailyRate, decimal LiabilityAmount, string Currency);

public record PayrollExportRequest(int Year, int Month, List<long>? StaffIds, string Format); // Format: belina, pastel, generic_csv, generic_pdf
public record PayrollExportResultDto(string FileName, string FileUrl, int TotalEmployees, decimal TotalGross, string Currency, DateTime ExportedAt, long ExportedByUserId, string Format);
