using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Hostel.Entities;

// Blocks, rooms and beds with capacity and gender designation
public class HostelBlock : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. Shumba House
    public string Code { get; set; } = null!; // SHU
    public string GenderDesignation { get; set; } = "male"; // male, female, mixed
    public int Capacity { get; set; } // total beds
    public int TotalRooms { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string? Location { get; set; }

    public long? HouseMasterStaffId { get; set; }
    public long? MatronStaffId { get; set; }

    public ICollection<HostelRoom> Rooms { get; set; } = new List<HostelRoom>();
    public ICollection<BlockStaffAssignment> StaffAssignments { get; set; } = new List<BlockStaffAssignment>();
}

public class HostelRoom : TenantOwnedEntity
{
    public long BlockId { get; set; }
    public HostelBlock Block { get; set; } = null!;
    public string RoomNumber { get; set; } = null!; // e.g. 101, A1
    public int Floor { get; set; } = 0;
    public int Capacity { get; set; } // number of beds
    public string GenderDesignation { get; set; } = "male";
    public bool IsActive { get; set; } = true;
    public string? Facilities { get; set; } // e.g. ensuite, common bathroom

    public ICollection<HostelBed> Beds { get; set; } = new List<HostelBed>();
}

public class HostelBed : TenantOwnedEntity
{
    public long RoomId { get; set; }
    public HostelRoom Room { get; set; } = null!;
    public long BlockId { get; set; } // denormalized for fast queries
    public string BedNumber { get; set; } = null!; // e.g. 101A, 101B, or 1,2,3
    public string Status { get; set; } = "available"; // available, occupied, reserved, maintenance, out_of_order
    public string Condition { get; set; } = "good"; // good, fair, damaged
    public long? CurrentStudentId { get; set; }
    public long? CurrentAllocationId { get; set; }
}

// House masters and matrons assigned to blocks
public class BlockStaffAssignment : TenantOwnedEntity
{
    public long BlockId { get; set; }
    public HostelBlock Block { get; set; } = null!;
    public long StaffId { get; set; }
    public string Role { get; set; } = "house_master"; // house_master, matron, assistant, prefect
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Responsibilities { get; set; }
}

// Allocation of learners to beds for a term with conflict detection and waiting list
public class BedAllocation : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long BedId { get; set; }
    public HostelBed Bed { get; set; } = null!;
    public long RoomId { get; set; }
    public long BlockId { get; set; }
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public DateTime AllocationDate { get; set; } = DateTime.UtcNow;
    public long AllocatedByUserId { get; set; }
    public string Status { get; set; } = "active"; // active, vacated, transferred, expired
    public DateTime? VacatedDate { get; set; }
    public string? VacatedReason { get; set; }
    public bool FeeApplied { get; set; } = false;
    public long? FeeStructureItemId { get; set; }
    public long? WaitingListId { get; set; } // if allocated from waiting list
}

public class WaitingList : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long? PreferredBlockId { get; set; }
    public long? PreferredRoomId { get; set; }
    public string Gender { get; set; } = "male";
    public long AcademicYearId { get; set; }
    public long TermId { get; set; }
    public int Priority { get; set; } = 0; // 0 = high, 1 = normal, etc.
    public int QueuePosition { get; set; }
    public string Status { get; set; } = "waiting"; // waiting, allocated, cancelled, expired
    public string? Reason { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
    public long RequestedByUserId { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public long? AllocatedBedId { get; set; }
}

// Boarding fees integrated with existing fee structure
public class BoardingFeeLink : TenantOwnedEntity
{
    public long BedAllocationId { get; set; }
    public BedAllocation Allocation { get; set; } = null!;
    public long FeeItemId { get; set; } // BOARDING fee item
    public long FeeStructureId { get; set; }
    public long FeeStructureItemId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}

// Exeat and leave register recording departure, expected return, actual return and authorising person
public class ExeatRegister : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long BlockId { get; set; }
    public long? BedAllocationId { get; set; }
    public string LeaveType { get; set; } = "exeat"; // exeat, weekend, medical, emergency, holiday
    public string Reason { get; set; } = null!;
    public DateTime DepartureDateTime { get; set; }
    public DateTime ExpectedReturnDateTime { get; set; }
    public DateTime? ActualReturnDateTime { get; set; }
    public string Status { get; set; } = "approved"; // pending, approved, departed, returned, overdue, cancelled
    public long AuthorisedByUserId { get; set; } // authorising person
    public string AuthoriserRole { get; set; } = "house_master"; // house_master, head, matron
    public long? ApprovedByUserId { get; set; }
    public string? ContactPhoneDuringLeave { get; set; }
    public string? DestinationAddress { get; set; }
    public string? AccompanyingPerson { get; set; }
    public bool IsOverdue => Status == "departed" && DateTime.UtcNow > ExpectedReturnDateTime && ActualReturnDateTime == null;
}

// Nightly roll call
public class RollCall : TenantOwnedEntity
{
    public long BlockId { get; set; }
    public long? RoomId { get; set; }
    public DateTime RollCallDate { get; set; } // date of roll call
    public string RollCallType { get; set; } = "nightly"; // nightly, morning, evening
    public string Status { get; set; } = "in_progress"; // in_progress, completed, cancelled
    public long ConductedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalExpected { get; set; }
    public int TotalPresent { get; set; }
    public int TotalAbsent { get; set; }
    public int TotalOnLeave { get; set; }

    public ICollection<RollCallEntry> Entries { get; set; } = new List<RollCallEntry>();
}

public class RollCallEntry : TenantOwnedEntity
{
    public long RollCallId { get; set; }
    public RollCall RollCall { get; set; } = null!;
    public long BlockId { get; set; }
    public long? RoomId { get; set; }
    public long BedId { get; set; }
    public long StudentId { get; set; }
    public DateTime RollCallDate { get; set; }
    public string Status { get; set; } = "present"; // present, absent, on_leave, sick_bay
    public string? Notes { get; set; }
    public long MarkedByUserId { get; set; }
}

// Visitor log
public class VisitorLog : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long BlockId { get; set; }
    public string VisitorName { get; set; } = null!;
    public string Relationship { get; set; } = null!; // parent, guardian, sibling, friend, other
    public string IdNumber { get; set; } = null!; // national ID
    public string Phone { get; set; } = null!;
    public DateTime CheckInDateTime { get; set; }
    public DateTime? CheckOutDateTime { get; set; }
    public string Purpose { get; set; } = null!;
    public long AuthorisedByUserId { get; set; } // house master authorising
    public string? Belongings { get; set; }
    public string Status { get; set; } = "checked_in"; // checked_in, checked_out, denied
}

// Incident and sick bay records with appropriate access restrictions
public class IncidentRecord : TenantOwnedEntity
{
    public long BlockId { get; set; }
    public long? RoomId { get; set; }
    public long StudentId { get; set; }
    public string IncidentType { get; set; } = "incident"; // incident, sick_bay, disciplinary, medical, welfare
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Severity { get; set; } = "low"; // low, medium, high, critical
    public DateTime IncidentDateTime { get; set; }
    public long ReportedByUserId { get; set; }
    public string? ActionTaken { get; set; }
    public string? FollowUpRequired { get; set; }
    public string Status { get; set; } = "open"; // open, investigating, resolved, closed
    public string Visibility { get; set; } = "house_master"; // house_master, matron, nurse, head, admin - access restrictions
    public bool IsConfidential { get; set; } = false;
    public long? AssignedToUserId { get; set; }
}

public class SickBayRecord : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long BlockId { get; set; }
    public DateTime CheckInDateTime { get; set; }
    public DateTime? CheckOutDateTime { get; set; }
    public string Symptoms { get; set; } = null!;
    public string? Diagnosis { get; set; }
    public string? Treatment { get; set; }
    public string? Medication { get; set; }
    public string Status { get; set; } = "admitted"; // admitted, treated, discharged, referred
    public long AdmittedByUserId { get; set; } // matron/nurse
    public long? DischargedByUserId { get; set; }
    public string Visibility { get; set; } = "matron"; // matron, nurse, house_master, head - access restrictions
    public bool IsConfidential { get; set; } = true;
    public string? Notes { get; set; }
}
