namespace LearnCloud.Library.DTOs;

public record LibraryCategoryDto(long Id, string Name, string Code, string? Description, long? ParentId, bool IsActive);
public record CreateCategoryRequest(string Name, string Code, string Type, long? ParentId, string? Description, string? GlCode);

public record BookDto(long Id, string Title, string Author, string? ISBN, long CategoryId, string CategoryName, string ShelfLocation, int TotalCopies, int AvailableCopies, decimal ReplacementPrice, string Currency, string Status, string? CoverImageUrl);
public record CreateBookRequest(string Title, string Author, string? ISBN, long CategoryId, string ShelfLocation, int TotalCopies, decimal ReplacementPrice, string Currency, string? Publisher, int? PublicationYear, string? Description);

public record BookCopyDto(long Id, long BookId, string BookTitle, string AccessionNumber, string Barcode, string ShelfLocation, string Status, string Condition, DateTime? LastIssuedAt);
public record CreateBookCopyRequest(long BookId, string AccessionNumber, string Barcode, string ShelfLocation, string Condition);

public record MembershipConfigDto(long Id, string MembershipType, int MaxBooks, int LoanPeriodDays, int MaxRenewals, decimal FinePerDay, string Currency, bool AllowReservations);
public record CreateMembershipConfigRequest(string MembershipType, int MaxBooks, int LoanPeriodDays, int MaxRenewals, decimal FinePerDay, string Currency, bool AllowReservations);

public record LibraryMemberDto(long Id, string MembershipNumber, string MemberType, long? StudentId, long? StaffId, string FullName, string Status, int CurrentlyBorrowed, decimal OutstandingFines, string Currency);
public record CreateMemberRequest(string MemberType, long? StudentId, long? StaffId, string FullName);

public record IssueRequest(string BarcodeOrAccession, long MemberId, long? IssuedByUserId); // fast issue by barcode or typing accession
public record ReturnRequest(string BarcodeOrAccession, long? ReturnedByUserId, string? Condition);

public record LoanDto(long Id, long BookCopyId, string BookTitle, string AccessionNumber, string Barcode, long MemberId, string MemberName, DateTime IssueDate, DateTime DueDate, DateTime? ReturnDate, string Status, int RenewalCount, bool IsOverdue, int DaysOverdue);
public record RenewRequest(long LoanId, string? Reason);

public record ReservationDto(long Id, long BookId, string BookTitle, long MemberId, string MemberName, DateTime ReservationDate, DateTime? ExpiryDate, string Status, int QueuePosition);
public record CreateReservationRequest(long BookId, long MemberId);

public record FineDto(long Id, long LoanId, string BookTitle, long MemberId, string MemberName, string FineType, decimal Amount, string Currency, int DaysOverdue, string Status, bool PostedToFeeAccount, long? FeeInvoiceId);
public record PayFineRequest(long FineId, bool PostToFeeAccount);

public record LostDamagedRequest(long BookCopyId, long LoanId, string Type, string Condition, decimal ReplacementCharge, decimal FineAmount, string Currency);

public record StockTakeDto(long Id, string Name, DateTime StartedAt, DateTime? CompletedAt, string Status, int TotalExpected, int TotalCounted, int Discrepancies, long CreatedByUserId);
public record CreateStockTakeRequest(string Name);
public record StockTakeItemDto(long Id, long StockTakeId, long BookCopyId, string AccessionNumber, string BookTitle, string ExpectedStatus, string CountedStatus, string DiscrepancyType, string? Notes);
public record CountStockTakeItemRequest(long BookCopyId, string CountedStatus, string? Notes);

public record CirculationReportDto(DateTime FromDate, DateTime ToDate, int TotalIssues, int TotalReturns, int TotalRenewals, List<DailyCirculationDto> Daily);
public record DailyCirculationDto(DateTime Date, int Issues, int Returns);
public record PopularTitleDto(long BookId, string Title, string Author, int TimesBorrowed, int AvailableCopies, int TotalCopies);
public record OverdueReportDto(List<LoanDto> OverdueLoans, decimal TotalFines, string Currency);
public record InventoryValueDto(int TotalTitles, int TotalCopies, int AvailableCopies, decimal TotalValue, string Currency, List<CategoryValueDto> ByCategory);
public record CategoryValueDto(long CategoryId, string CategoryName, int Copies, decimal Value, string Currency);

public record FastIssueResponse(bool Success, LoanDto? Loan, string Message, string? Barcode, long? MemberId);
public record FastReturnResponse(bool Success, LoanDto? Loan, FineDto? FineGenerated, string Message);
