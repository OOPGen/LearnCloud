using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Library.Entities;

// Catalogue with title, author, ISBN, category, copies and shelf location
public class LibraryCategory : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // Fiction, Non-Fiction, Textbooks, Reference, Science
    public string Code { get; set; } = null!; // FIC, NONFIC, TEXT
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public long? ParentCategoryId { get; set; }
}

public class Book : TenantOwnedEntity
{
    public string Title { get; set; } = null!;
    public string Author { get; set; } = null!; // could be multiple authors comma separated
    public string? ISBN { get; set; } // 13 digits
    public long CategoryId { get; set; }
    public LibraryCategory Category { get; set; } = null!;
    public string? Publisher { get; set; }
    public int? PublicationYear { get; set; }
    public string? Edition { get; set; }
    public string ShelfLocation { get; set; } = null!; // e.g. A-1-3 (Block-Shelf-Position)
    public int TotalCopies { get; set; } // cached count of copies
    public int AvailableCopies { get; set; } // cached available
    public decimal ReplacementPrice { get; set; } // for lost/damaged charge
    public string Currency { get; set; } = "USD";
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string Status { get; set; } = "active"; // active, archived

    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();
}

// Barcode or accession number per copy
public class BookCopy : TenantOwnedEntity
{
    public long BookId { get; set; }
    public Book Book { get; set; } = null!;
    public string AccessionNumber { get; set; } = null!; // e.g. ACC-2026-00001 unique per tenant
    public string Barcode { get; set; } = null!; // e.g. 978... or generated barcode 1234567890123, unique per tenant
    public string ShelfLocation { get; set; } = null!; // per copy location, may override book shelf
    public string Status { get; set; } = "available"; // available, issued, reserved, lost, damaged, under_maintenance
    public string Condition { get; set; } = "good"; // new, good, fair, poor, damaged
    public DateTime? LastIssuedAt { get; set; }
    public DateTime? LastReturnedAt { get; set; }
    public long? CurrentLoanId { get; set; }
}

// Membership derived from learners and staff with configurable borrowing limits and loan periods
public class MembershipConfig : TenantOwnedEntity
{
    public string MembershipType { get; set; } = null!; // student, staff, teacher
    public int MaxBooks { get; set; } = 3; // borrowing limits
    public int LoanPeriodDays { get; set; } = 14; // loan periods
    public int MaxRenewals { get; set; } = 1;
    public decimal FinePerDay { get; set; } = 1.00m; // overdue fine per day
    public string Currency { get; set; } = "USD";
    public decimal MaxFine { get; set; } = 50.00m; // cap
    public bool AllowReservations { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class LibraryMember : TenantOwnedEntity
{
    public string MembershipNumber { get; set; } = null!; // LIB-2026-00001
    public string MemberType { get; set; } = null!; // student, staff, teacher
    public long? StudentId { get; set; } // if student
    public long? StaffId { get; set; } // if staff
    public long? UserId { get; set; } // linked user account
    public string FullName { get; set; } = null!;
    public string Status { get; set; } = "active"; // active, suspended, graduated, inactive
    public int CurrentlyBorrowed { get; set; } = 0;
    public int TotalBorrowed { get; set; } = 0;
    public decimal OutstandingFines { get; set; } = 0m;
    public string Currency { get; set; } = "USD";
    public DateTime? MembershipExpiryDate { get; set; }
}

// Issue and return with due dates
public class Loan : TenantOwnedEntity
{
    public long BookCopyId { get; set; }
    public BookCopy BookCopy { get; set; } = null!;
    public long BookId { get; set; }
    public long MemberId { get; set; }
    public LibraryMember Member { get; set; } = null!;
    public long? StudentId { get; set; } // denormalized for fine posting to fee account
    public long? StaffId { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }

    public string Status { get; set; } = "issued"; // issued, returned, overdue, lost, damaged
    public long IssuedByUserId { get; set; }
    public long? ReturnedByUserId { get; set; }

    public int RenewalCount { get; set; } = 0;
    public bool IsOverdue => Status == "overdue" || (Status == "issued" && DateTime.UtcNow.Date > DueDate.Date);

    public ICollection<LoanRenewal> Renewals { get; set; } = new List<LoanRenewal>();
}

// Renewals
public class LoanRenewal : TenantOwnedEntity
{
    public long LoanId { get; set; }
    public Loan Loan { get; set; } = null!;
    public DateTime RenewalDate { get; set; } = DateTime.UtcNow;
    public DateTime PreviousDueDate { get; set; }
    public DateTime NewDueDate { get; set; }
    public int RenewalNumber { get; set; }
    public long ApprovedByUserId { get; set; }
    public string? Reason { get; set; }
}

// Reservations and waiting list
public class Reservation : TenantOwnedEntity
{
    public long BookId { get; set; }
    public Book Book { get; set; } = null!;
    public long MemberId { get; set; }
    public LibraryMember Member { get; set; } = null!;
    public long? StudentId { get; set; }

    public DateTime ReservationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiryDate { get; set; } // reservation expires if not fulfilled
    public string Status { get; set; } = "pending"; // pending, fulfilled, cancelled, expired
    public int QueuePosition { get; set; } // waiting list position
    public DateTime? FulfilledAt { get; set; }
    public long? FulfilledLoanId { get; set; }
}

// Overdue tracking with fines that post to learner's fee account through existing fee services
public class Fine : TenantOwnedEntity
{
    public long LoanId { get; set; }
    public Loan Loan { get; set; } = null!;
    public long MemberId { get; set; }
    public long? StudentId { get; set; } // for posting to fee account

    public string FineType { get; set; } = "overdue"; // overdue, lost, damaged
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public int DaysOverdue { get; set; }
    public decimal FinePerDay { get; set; }

    public string Status { get; set; } = "pending"; // pending, posted_to_fee_account, paid, waived
    public bool PostedToFeeAccount { get; set; } = false;
    public long? FeeInvoiceId { get; set; } // link to fee invoice created via existing fee services
    public long? FeeInvoiceItemId { get; set; }

    public DateTime? PaidAt { get; set; }
    public string? WaivedReason { get; set; }
}

// Lost and damaged handling with replacement charge
public class LostDamagedRecord : TenantOwnedEntity
{
    public long BookCopyId { get; set; }
    public BookCopy BookCopy { get; set; } = null!;
    public long LoanId { get; set; }
    public Loan Loan { get; set; } = null!;
    public long MemberId { get; set; }

    public string Type { get; set; } = "lost"; // lost, damaged
    public string Condition { get; set; } = null!; // damaged details
    public decimal ReplacementCharge { get; set; }
    public decimal FineAmount { get; set; } // overdue fine + replacement
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending"; // pending, charged, paid, waived
    public long? FeeInvoiceId { get; set; }
}

// Stock take with discrepancy report
public class StockTake : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. Stock Take Term 2 2026
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "in_progress"; // in_progress, completed, cancelled
    public long CreatedByUserId { get; set; }
    public int TotalExpected { get; set; }
    public int TotalCounted { get; set; }
    public int Discrepancies { get; set; }

    public ICollection<StockTakeItem> Items { get; set; } = new List<StockTakeItem>();
}

public class StockTakeItem : TenantOwnedEntity
{
    public long StockTakeId { get; set; }
    public StockTake StockTake { get; set; } = null!;
    public long BookCopyId { get; set; }
    public BookCopy BookCopy { get; set; } = null!;

    public string ExpectedStatus { get; set; } = null!; // what system thinks
    public string CountedStatus { get; set; } = null!; // what was counted
    public string DiscrepancyType { get; set; } = "none"; // none, missing, extra, damaged, wrong_location
    public string? Notes { get; set; }
    public long CountedByUserId { get; set; }
}
