using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Transport.Entities;

// Routes with ordered stops and expected times
public class Route : TenantOwnedEntity
{
    public string Name { get; set; } = null!; // e.g. Hillside Route A
    public string Code { get; set; } = null!; // RTE-A
    public string? Description { get; set; }
    public string Direction { get; set; } = "both"; // morning, evening, both
    public bool IsActive { get; set; } = true;
    public long? AcademicYearId { get; set; }
    public long? TermId { get; set; }
    public decimal TotalDistanceKm { get; set; } = 0m;
    public int EstimatedDurationMinutes { get; set; } = 60;
    public decimal FeeAmount { get; set; } // transport fee per term for this route
    public string Currency { get; set; } = "USD";

    public long? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public long? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public long? AssistantId { get; set; }
    public Driver? Assistant { get; set; }

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
    public ICollection<TransportAssignment> Assignments { get; set; } = new List<TransportAssignment>();
}

public class RouteStop : TenantOwnedEntity
{
    public long RouteId { get; set; }
    public Route Route { get; set; } = null!;
    public string Name { get; set; } = null!; // e.g. Hillside Shops, Bulawayo CBD
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int OrderNumber { get; set; } // ordered stops 1,2,3...
    public TimeSpan? ExpectedArrivalTime { get; set; } // e.g. 06:30
    public TimeSpan? ExpectedDepartureTime { get; set; }
    public decimal DistanceFromStartKm { get; set; } = 0m;
    public int EstimatedMinutesFromStart { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

// Vehicles with capacity, registration, insurance and licence expiry with reminders
public class Vehicle : TenantOwnedEntity
{
    public string RegistrationNumber { get; set; } = null!; // e.g. ACD 1234
    public string Make { get; set; } = null!; // Toyota, Hino
    public string Model { get; set; } = null!; // Coaster
    public int Capacity { get; set; } // seats
    public int Year { get; set; }
    public string? FuelType { get; set; }
    public DateTime InsuranceExpiry { get; set; }
    public DateTime LicenceExpiry { get; set; } // vehicle licence (ZINARA)
    public DateTime? FitnessExpiry { get; set; } // fitness certificate
    public DateTime? ServiceDueDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public string Status { get; set; } = "active"; // active, maintenance, retired

    // For reminders
    public DateTime? LastInsuranceReminderSentAt { get; set; }
    public DateTime? LastLicenceReminderSentAt { get; set; }
}

// Drivers and assistants with licence expiry tracking
public class Driver : TenantOwnedEntity
{
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = "driver"; // driver, assistant
    public long? StaffId { get; set; } // nullable link to staff if driver is also staff
    public string? LicenceNumber { get; set; }
    public string? LicenceType { get; set; } // Class 2, Class 4, etc.
    public DateTime? LicenceExpiry { get; set; }
    public DateTime? MedicalExpiry { get; set; } // medical certificate
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? IdNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime? LastLicenceReminderSentAt { get; set; }
}

// Learner assignment to a route and stop with capacity enforcement
public class TransportAssignment : TenantOwnedEntity
{
    public long StudentId { get; set; }
    public long RouteId { get; set; }
    public Route Route { get; set; } = null!;
    public long PickupStopId { get; set; }
    public RouteStop PickupStop { get; set; } = null!;
    public long? DropStopId { get; set; } // may be different for return trip
    public RouteStop? DropStop { get; set; }

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    public long AssignedByUserId { get; set; }
    public string Status { get; set; } = "active"; // active, inactive, suspended
    public DateTime? UnassignedDate { get; set; }
    public string? UnassignedReason { get; set; }

    public long AcademicYearId { get; set; }
    public long TermId { get; set; }

    // For fee integration
    public long? FeeStructureItemId { get; set; } // link to fee structure item created for transport fee
    public bool FeeApplied { get; set; } = false;
}

// Transport fees that flow into existing fee structure and invoicing rather than parallel billing
// We reuse FeeItem with code TRANSPORT and FeeStructureItem, but track link
public class TransportFeeLink : TenantOwnedEntity
{
    public long TransportAssignmentId { get; set; }
    public TransportAssignment Assignment { get; set; } = null!;
    public long FeeItemId { get; set; } // should be TRANSPORT fee item
    public long FeeStructureId { get; set; } // structure that includes transport fee for this learner
    public long FeeStructureItemId { get; set; } // item within structure
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}

// Boarding attendance per trip if school wants it
public class TransportAttendance : TenantOwnedEntity
{
    public long RouteId { get; set; }
    public long? RouteStopId { get; set; }
    public long StudentId { get; set; }
    public long TransportAssignmentId { get; set; }
    public DateTime TripDate { get; set; } // date of trip
    public string TripType { get; set; } = "morning"; // morning, evening
    public string Status { get; set; } = "boarded"; // boarded, missed, absent, excused
    public TimeSpan? ActualBoardingTime { get; set; }
    public long MarkedByUserId { get; set; }
    public string? Notes { get; set; }
}

// For route change and absence notifications using existing messaging module
public class TransportNotificationLog : TenantOwnedEntity
{
    public long RouteId { get; set; }
    public long? StudentId { get; set; }
    public string NotificationType { get; set; } = null!; // route_change, absence, assignment, unassignment
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public long? MessageBatchId { get; set; } // link to messaging module batch
    public string? RecipientsJson { get; set; } // list of guardian ids
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public long SentByUserId { get; set; }
}
