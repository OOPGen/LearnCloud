using LearnCloud.Transport.DTOs;
using Microsoft.Extensions.Logging;
using LearnCloud.Transport.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Transport.Services;

public interface ITransportService
{
    // Routes with ordered stops and expected times
    Task<RouteDto> CreateRouteAsync(long tenantId, long userId, CreateRouteRequest req, CancellationToken ct = default);
    Task<List<RouteDto>> GetRoutesAsync(long tenantId, CancellationToken ct = default);
    Task<RouteDto> GetRouteAsync(long tenantId, long routeId, CancellationToken ct = default);
    Task<RouteStopDto> AddStopAsync(long tenantId, long userId, long routeId, CreateRouteStopRequest req, CancellationToken ct = default);
    Task<List<RouteStopDto>> GetStopsAsync(long tenantId, long routeId, CancellationToken ct = default);
    Task ReorderStopsAsync(long tenantId, long routeId, ReorderStopsRequest req, CancellationToken ct = default);

    // Vehicles with capacity, registration, insurance and licence expiry with reminders
    Task<VehicleDto> CreateVehicleAsync(long tenantId, long userId, CreateVehicleRequest req, CancellationToken ct = default);
    Task<List<VehicleDto>> GetVehiclesAsync(long tenantId, CancellationToken ct = default);
    Task<List<VehicleDto>> GetVehiclesNeedingReminderAsync(long tenantId, CancellationToken ct = default);

    // Drivers and assistants with licence expiry tracking
    Task<DriverDto> CreateDriverAsync(long tenantId, long userId, CreateDriverRequest req, CancellationToken ct = default);
    Task<List<DriverDto>> GetDriversAsync(long tenantId, string? role, CancellationToken ct = default);
    Task<List<DriverDto>> GetDriversNeedingLicenceReminderAsync(long tenantId, CancellationToken ct = default);

    // Learner assignment to a route and stop with capacity enforcement
    Task<TransportAssignmentDto> AssignLearnerAsync(long tenantId, long userId, AssignLearnerRequest req, CancellationToken ct = default);
    Task<List<TransportAssignmentDto>> BulkAssignAsync(long tenantId, long userId, BulkAssignRequest req, CancellationToken ct = default);
    Task UnassignLearnerAsync(long tenantId, long userId, long assignmentId, UnassignLearnerRequest req, CancellationToken ct = default);
    Task<List<TransportAssignmentDto>> GetAssignmentsByRouteAsync(long tenantId, long routeId, CancellationToken ct = default);

    // Transport fees that flow into existing fee structure and invoicing rather than parallel billing
    Task ApplyTransportFeeAsync(long tenantId, long userId, long assignmentId, CancellationToken ct = default);

    // Boarding attendance per trip
    Task<List<TransportAttendanceDto>> MarkBoardingAsync(long tenantId, long userId, MarkBoardingRequest req, CancellationToken ct = default);
    Task<List<TransportAttendanceDto>> GetBoardingAttendanceAsync(long tenantId, long routeId, DateTime tripDate, string? tripType, CancellationToken ct = default);

    // Reports
    Task<List<UtilisationReportDto>> GetUtilisationReportAsync(long tenantId, CancellationToken ct = default);
    Task<List<RevenueReportDto>> GetRevenueReportAsync(long tenantId, long academicYearId, long termId, CancellationToken ct = default);
    Task<UnassignedLearnersDto> GetUnassignedLearnersAsync(long tenantId, long? gradeId, CancellationToken ct = default);

    // Printable route manifest for each driver
    Task<RouteManifestDto> GetRouteManifestAsync(long tenantId, long routeId, CancellationToken ct = default);

    // Notifications using existing messaging module
    Task NotifyRouteChangeAsync(long tenantId, long userId, long routeId, string changeDescription, CancellationToken ct = default);
    Task NotifyAbsenceAsync(long tenantId, long userId, long routeId, long studentId, DateTime tripDate, CancellationToken ct = default);
}

public class TransportService : ITransportService
{
    private readonly LearnCloudDbContext _db;
    private readonly Fees.Services.FeeCalculationService _feeCalc;
    private readonly ILogger<TransportService> _logger;

    public TransportService(LearnCloudDbContext db, Fees.Services.FeeCalculationService feeCalc, ILogger<TransportService> logger)
    {
        _db = db;
        _feeCalc = feeCalc;
        _logger = logger;
    }

    public async Task<RouteDto> CreateRouteAsync(long tenantId, long userId, CreateRouteRequest req, CancellationToken ct = default)
    {
        var route = new Route
        {
            TenantId = tenantId,
            Name = req.Name,
            Code = req.Code,
            Description = req.Description,
            Direction = req.Direction,
            AcademicYearId = req.AcademicYearId,
            TermId = req.TermId,
            TotalDistanceKm = req.TotalDistanceKm,
            EstimatedDurationMinutes = req.EstimatedDurationMinutes,
            FeeAmount = req.FeeAmount,
            Currency = req.Currency,
            VehicleId = req.VehicleId,
            DriverId = req.DriverId,
            AssistantId = req.AssistantId,
            IsActive = true,
            CreatedBy = userId
        };
        _db.Set<Route>().Add(route);
        await _db.SaveChangesAsync(ct);
        return await GetRouteAsync(tenantId, route.Id, ct);
    }

    public async Task<List<RouteDto>> GetRoutesAsync(long tenantId, CancellationToken ct = default)
    {
        // PERFORMANCE FIX C1: Was N+1 - CountAsync per route + FirstOrDefault per driver/assistant = 1 + 2N queries
        // Fixed: Single query for assigned counts via GROUP BY + single query for all drivers/assistants

        var routes = await _db.Set<Route>().Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Vehicle)
            .Include(r => r.Stops)
            .AsNoTracking()
            .ToListAsync(ct);

        if (routes.Count == 0) return new List<RouteDto>();

        var routeIds = routes.Select(r => r.Id).ToList();

        // Single query for all assigned counts GROUP BY RouteId (was CountAsync per route in loop)
        var assignedCounts = await _db.Set<TransportAssignment>()
            .Where(a => a.TenantId == tenantId && routeIds.Contains(a.RouteId) && a.Status == "active" && !a.IsDeleted)
            .GroupBy(a => a.RouteId)
            .Select(g => new { RouteId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RouteId, x => x.Count, ct);

        // Single query for all drivers and assistants (was FirstOrDefault per route per driver)
        var driverIds = routes.Where(r => r.DriverId.HasValue).Select(r => r.DriverId!.Value)
            .Concat(routes.Where(r => r.AssistantId.HasValue).Select(r => r.AssistantId!.Value))
            .Distinct()
            .ToList();

        var driversDict = new Dictionary<long, Driver>();
        if (driverIds.Count > 0)
        {
            driversDict = await _db.Set<Driver>()
                .Where(d => driverIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);
        }

        var result = new List<RouteDto>(routes.Count);
        foreach (var r in routes)
        {
            assignedCounts.TryGetValue(r.Id, out var assigned);
            var vehicle = r.Vehicle;
            var capacity = vehicle?.Capacity ?? 0;
            var utilisation = capacity > 0 ? Math.Round((decimal)assigned / capacity * 100, 1) : 0m;
            
            driversDict.TryGetValue(r.DriverId ?? -1, out var driver);
            driversDict.TryGetValue(r.AssistantId ?? -1, out var assistant);

            result.Add(new RouteDto(r.Id, r.Name, r.Code, r.Description, r.Direction, r.IsActive, r.AcademicYearId, r.TermId, r.TotalDistanceKm, r.EstimatedDurationMinutes, r.FeeAmount, r.Currency, r.VehicleId, vehicle?.RegistrationNumber, r.DriverId, driver?.FullName, r.AssistantId, assistant?.FullName, r.Stops.Count, assigned, capacity, utilisation));
        }
        return result;
    }

    public async Task<RouteDto> GetRouteAsync(long tenantId, long routeId, CancellationToken ct = default)
    {
        var r = await _db.Set<Route>().Where(x => x.Id == routeId && x.TenantId == tenantId && !x.IsDeleted).Include(x => x.Vehicle).Include(x => x.Stops).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Route not found");
        var assigned = await _db.Set<TransportAssignment>().CountAsync(a => a.TenantId == tenantId && a.RouteId == r.Id && a.Status == "active" && !a.IsDeleted, ct);
        var capacity = r.Vehicle?.Capacity ?? 0;
        var utilisation = capacity > 0 ? Math.Round((decimal)assigned / capacity * 100, 1) : 0m;
        var driver = r.DriverId.HasValue ? await _db.Set<Driver>().FirstOrDefaultAsync(d => d.Id == r.DriverId.Value, ct) : null;
        var assistant = r.AssistantId.HasValue ? await _db.Set<Driver>().FirstOrDefaultAsync(d => d.Id == r.AssistantId.Value, ct) : null;
        return new RouteDto(r.Id, r.Name, r.Code, r.Description, r.Direction, r.IsActive, r.AcademicYearId, r.TermId, r.TotalDistanceKm, r.EstimatedDurationMinutes, r.FeeAmount, r.Currency, r.VehicleId, r.Vehicle?.RegistrationNumber, r.DriverId, driver?.FullName, r.AssistantId, assistant?.FullName, r.Stops.Count, assigned, capacity, utilisation);
    }

    public async Task<RouteStopDto> AddStopAsync(long tenantId, long userId, long routeId, CreateRouteStopRequest req, CancellationToken ct = default)
    {
        var route = await _db.Set<Route>().FirstOrDefaultAsync(r => r.Id == routeId && r.TenantId == tenantId && !r.IsDeleted, ct) ?? throw new InvalidOperationException("Route not found");

        var stop = new RouteStop
        {
            TenantId = tenantId,
            RouteId = routeId,
            Name = req.Name,
            Address = req.Address,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            OrderNumber = req.OrderNumber,
            ExpectedArrivalTime = string.IsNullOrEmpty(req.ExpectedArrivalTime) ? null : TimeSpan.Parse(req.ExpectedArrivalTime),
            ExpectedDepartureTime = string.IsNullOrEmpty(req.ExpectedDepartureTime) ? null : TimeSpan.Parse(req.ExpectedDepartureTime),
            DistanceFromStartKm = req.DistanceFromStartKm,
            EstimatedMinutesFromStart = req.EstimatedMinutesFromStart,
            IsActive = true,
            CreatedBy = userId
        };
        _db.Set<RouteStop>().Add(stop);
        await _db.SaveChangesAsync(ct);

        return new RouteStopDto(stop.Id, stop.RouteId, stop.Name, stop.Address, stop.Latitude, stop.Longitude, stop.OrderNumber, stop.ExpectedArrivalTime?.ToString(@"hh\:mm"), stop.ExpectedDepartureTime?.ToString(@"hh\:mm"), stop.DistanceFromStartKm, stop.EstimatedMinutesFromStart, stop.IsActive);
    }

    public async Task<List<RouteStopDto>> GetStopsAsync(long tenantId, long routeId, CancellationToken ct = default)
    {
        var stops = await _db.Set<RouteStop>().Where(s => s.TenantId == tenantId && s.RouteId == routeId && !s.IsDeleted).OrderBy(s => s.OrderNumber).ToListAsync(ct);
        return stops.Select(s => new RouteStopDto(s.Id, s.RouteId, s.Name, s.Address, s.Latitude, s.Longitude, s.OrderNumber, s.ExpectedArrivalTime?.ToString(@"hh\:mm"), s.ExpectedDepartureTime?.ToString(@"hh\:mm"), s.DistanceFromStartKm, s.EstimatedMinutesFromStart, s.IsActive)).ToList();
    }

    public async Task ReorderStopsAsync(long tenantId, long routeId, ReorderStopsRequest req, CancellationToken ct = default)
    {
        var stops = await _db.Set<RouteStop>().Where(s => s.TenantId == tenantId && s.RouteId == routeId && !s.IsDeleted).ToListAsync(ct);
        for (int i = 0; i < req.OrderedStopIds.Count; i++)
        {
            var stop = stops.FirstOrDefault(s => s.Id == req.OrderedStopIds[i]);
            if (stop != null) stop.OrderNumber = i + 1;
        }
        await _db.SaveChangesAsync(ct);

        // Notify route change using existing messaging module
        await NotifyRouteChangeAsync(tenantId, 0, routeId, $"Stops reordered for route {routeId}", ct);
    }

    public async Task<VehicleDto> CreateVehicleAsync(long tenantId, long userId, CreateVehicleRequest req, CancellationToken ct = default)
    {
        var vehicle = new Vehicle
        {
            TenantId = tenantId,
            RegistrationNumber = req.RegistrationNumber,
            Make = req.Make,
            Model = req.Model,
            Capacity = req.Capacity,
            Year = req.Year,
            FuelType = req.FuelType,
            InsuranceExpiry = req.InsuranceExpiry,
            LicenceExpiry = req.LicenceExpiry,
            FitnessExpiry = req.FitnessExpiry,
            ServiceDueDate = req.ServiceDueDate,
            Notes = req.Notes,
            IsActive = true,
            Status = "active",
            CreatedBy = userId
        };
        _db.Set<Vehicle>().Add(vehicle);
        await _db.SaveChangesAsync(ct);
        return await MapVehicleAsync(vehicle, ct);
    }

    public async Task<List<VehicleDto>> GetVehiclesAsync(long tenantId, CancellationToken ct = default)
    {
        var list = await _db.Set<Vehicle>().Where(v => v.TenantId == tenantId && !v.IsDeleted).ToListAsync(ct);
        var result = new List<VehicleDto>();
        foreach (var v in list) result.Add(await MapVehicleAsync(v, ct));
        return result;
    }

    public async Task<List<VehicleDto>> GetVehiclesNeedingReminderAsync(long tenantId, CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow.AddDays(30); // reminder 30 days before expiry
        var list = await _db.Set<Vehicle>().Where(v => v.TenantId == tenantId && !v.IsDeleted && (v.InsuranceExpiry <= threshold || v.LicenceExpiry <= threshold)).ToListAsync(ct);
        var result = new List<VehicleDto>();
        foreach (var v in list) result.Add(await MapVehicleAsync(v, ct));
        return result.Where(v => v.NeedsInsuranceReminder || v.NeedsLicenceReminder).ToList();
    }

    private async Task<VehicleDto> MapVehicleAsync(Vehicle v, CancellationToken ct)
    {
        var daysToInsurance = (v.InsuranceExpiry.Date - DateTime.UtcNow.Date).Days;
        var daysToLicence = (v.LicenceExpiry.Date - DateTime.UtcNow.Date).Days;
        return new VehicleDto(v.Id, v.RegistrationNumber, v.Make, v.Model, v.Capacity, v.Year, v.FuelType, v.InsuranceExpiry, v.LicenceExpiry, v.FitnessExpiry, v.ServiceDueDate, v.IsActive, v.Status, daysToInsurance, daysToLicence, daysToInsurance <= 30, daysToLicence <= 30);
    }

    public async Task<DriverDto> CreateDriverAsync(long tenantId, long userId, CreateDriverRequest req, CancellationToken ct = default)
    {
        var driver = new Driver
        {
            TenantId = tenantId,
            FullName = req.FullName,
            Role = req.Role,
            StaffId = req.StaffId,
            LicenceNumber = req.LicenceNumber,
            LicenceType = req.LicenceType,
            LicenceExpiry = req.LicenceExpiry,
            MedicalExpiry = req.MedicalExpiry,
            Phone = req.Phone,
            Email = req.Email,
            IdNumber = req.IdNumber,
            Notes = req.Notes,
            IsActive = true,
            CreatedBy = userId
        };
        _db.Set<Driver>().Add(driver);
        await _db.SaveChangesAsync(ct);
        return await MapDriverAsync(driver, ct);
    }

    public async Task<List<DriverDto>> GetDriversAsync(long tenantId, string? role, CancellationToken ct = default)
    {
        var query = _db.Set<Driver>().Where(d => d.TenantId == tenantId && !d.IsDeleted);
        if (!string.IsNullOrEmpty(role)) query = query.Where(d => d.Role == role);
        var list = await query.ToListAsync(ct);
        var result = new List<DriverDto>();
        foreach (var d in list) result.Add(await MapDriverAsync(d, ct));
        return result;
    }

    public async Task<List<DriverDto>> GetDriversNeedingLicenceReminderAsync(long tenantId, CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow.AddDays(30);
        var list = await _db.Set<Driver>().Where(d => d.TenantId == tenantId && !d.IsDeleted && d.LicenceExpiry.HasValue && d.LicenceExpiry.Value <= threshold).ToListAsync(ct);
        var result = new List<DriverDto>();
        foreach (var d in list) result.Add(await MapDriverAsync(d, ct));
        return result.Where(d => d.NeedsLicenceReminder).ToList();
    }

    private Task<DriverDto> MapDriverAsync(Driver d, CancellationToken ct)
    {
        var daysToLicence = d.LicenceExpiry.HasValue ? (d.LicenceExpiry.Value.Date - DateTime.UtcNow.Date).Days : 999;
        return Task.FromResult(new DriverDto(d.Id, d.FullName, d.Role, d.StaffId, d.LicenceNumber, d.LicenceType, d.LicenceExpiry, d.MedicalExpiry, d.Phone, d.Email, d.IsActive, daysToLicence, daysToLicence <= 30));
    }

    public async Task<TransportAssignmentDto> AssignLearnerAsync(long tenantId, long userId, AssignLearnerRequest req, CancellationToken ct = default)
    {
        // Capacity enforcement
        var route = await _db.Set<Route>().Include(r => r.Vehicle).FirstOrDefaultAsync(r => r.Id == req.RouteId && r.TenantId == tenantId && !r.IsDeleted, ct) ?? throw new InvalidOperationException("Route not found");
        var vehicleCapacity = route.Vehicle?.Capacity ?? 0;
        var currentAssigned = await _db.Set<TransportAssignment>().CountAsync(a => a.TenantId == tenantId && a.RouteId == req.RouteId && a.Status == "active" && !a.IsDeleted, ct);

        if (vehicleCapacity > 0 && currentAssigned >= vehicleCapacity)
            throw new InvalidOperationException($"Route {route.Name} capacity {vehicleCapacity} reached, assigned {currentAssigned}, cannot assign more learners - capacity enforcement");

        // Check if learner already assigned to another route in same term
        var existingAssignment = await _db.Set<TransportAssignment>().FirstOrDefaultAsync(a => a.TenantId == tenantId && a.StudentId == req.StudentId && a.AcademicYearId == req.AcademicYearId && a.TermId == req.TermId && a.Status == "active" && !a.IsDeleted, ct);
        if (existingAssignment != null)
            throw new InvalidOperationException($"Learner already assigned to route {existingAssignment.RouteId} in same term, unassign first or transfer");

        var pickupStop = await _db.Set<RouteStop>().FirstOrDefaultAsync(s => s.Id == req.PickupStopId && s.TenantId == tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Pickup stop not found");
        RouteStop? dropStop = null;
        if (req.DropStopId.HasValue)
            dropStop = await _db.Set<RouteStop>().FirstOrDefaultAsync(s => s.Id == req.DropStopId.Value && s.TenantId == tenantId && !s.IsDeleted, ct);

        var assignment = new TransportAssignment
        {
            TenantId = tenantId,
            StudentId = req.StudentId,
            RouteId = req.RouteId,
            PickupStopId = req.PickupStopId,
            DropStopId = req.DropStopId,
            AssignedDate = DateTime.UtcNow,
            AssignedByUserId = userId,
            Status = "active",
            AcademicYearId = req.AcademicYearId,
            TermId = req.TermId,
            CreatedBy = userId
        };
        _db.Set<TransportAssignment>().Add(assignment);
        await _db.SaveChangesAsync(ct);

        // Transport fees flow into existing fee structure and invoicing rather than parallel billing
        await ApplyTransportFeeAsync(tenantId, userId, assignment.Id, ct);

        // Route change notification to guardians using existing messaging module
        await NotifyRouteChangeAsync(tenantId, userId, req.RouteId, $"Learner {req.StudentId} assigned to route {route.Name} pickup {pickupStop.Name}", ct);

        return await MapAssignmentAsync(assignment, ct);
    }

    public async Task<List<TransportAssignmentDto>> BulkAssignAsync(long tenantId, long userId, BulkAssignRequest req, CancellationToken ct = default)
    {
        var result = new List<TransportAssignmentDto>();
        foreach (var studentId in req.StudentIds)
        {
            try
            {
                var assignment = await AssignLearnerAsync(tenantId, userId, new AssignLearnerRequest(studentId, req.RouteId, req.PickupStopId, req.DropStopId, req.AcademicYearId, req.TermId), ct);
                result.Add(assignment);
            }
            catch (Exception ex)
            {
                // Log and continue for bulk
                _logger.LogWarning(ex, "Bulk assign failed for student {StudentId} route {RouteId}", studentId, req.RouteId);
            }
        }
        return result;
    }

    public async Task UnassignLearnerAsync(long tenantId, long userId, long assignmentId, UnassignLearnerRequest req, CancellationToken ct = default)
    {
        var assignment = await _db.Set<TransportAssignment>().FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId && !a.IsDeleted, ct) ?? throw new InvalidOperationException("Assignment not found");
        assignment.Status = "inactive";
        assignment.UnassignedDate = DateTime.UtcNow;
        assignment.UnassignedReason = req.Reason;
        assignment.UpdatedBy = userId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notify unassignment
        await NotifyRouteChangeAsync(tenantId, userId, assignment.RouteId, $"Learner {assignment.StudentId} unassigned from route {assignment.RouteId} reason {req.Reason}", ct);
    }

    public async Task<List<TransportAssignmentDto>> GetAssignmentsByRouteAsync(long tenantId, long routeId, CancellationToken ct = default)
    {
        var assignments = await _db.Set<TransportAssignment>().Where(a => a.TenantId == tenantId && a.RouteId == routeId && a.Status == "active" && !a.IsDeleted).ToListAsync(ct);
        var result = new List<TransportAssignmentDto>();
        foreach (var a in assignments) result.Add(await MapAssignmentAsync(a, ct));
        return result;
    }

    public async Task ApplyTransportFeeAsync(long tenantId, long userId, long assignmentId, CancellationToken ct = default)
    {
        var assignment = await _db.Set<TransportAssignment>().Include(a => a.Route).FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId && !a.IsDeleted, ct) ?? throw new InvalidOperationException("Assignment not found");
        var route = assignment.Route;

        if (assignment.FeeApplied) return; // already applied

        // Find or create fee item for transport
        var feeItem = await _db.Set<Fees.Entities.FeeItem>().FirstOrDefaultAsync(fi => fi.TenantId == tenantId && fi.Code == "TRANSPORT" && !fi.IsDeleted, ct);
        if (feeItem == null)
        {
            feeItem = new Fees.Entities.FeeItem { TenantId = tenantId, Name = "Transport", Code = "TRANSPORT", Recurrence = Fees.Entities.FeeItemRecurrence.PerTerm, IsProratable = false, IsOptional = false, Description = "Transport fee" };
            _db.Set<Fees.Entities.FeeItem>().Add(feeItem);
            await _db.SaveChangesAsync(ct);
        }

        // Find or create fee structure for this learner for year/term that includes transport
        // Priority: individual learner transport fee structure
        var existingStructure = await _db.Set<Fees.Entities.FeeStructure>().FirstOrDefaultAsync(fs => fs.TenantId == tenantId && fs.StudentId == assignment.StudentId && fs.AcademicYearId == assignment.AcademicYearId && fs.TermId == assignment.TermId && !fs.IsDeleted, ct);

        if (existingStructure == null)
        {
            // Create individual transport fee structure that will be merged with grade/stream structures during invoice generation (priority individual > stream > grade > school)
            existingStructure = new Fees.Entities.FeeStructure
            {
                TenantId = tenantId,
                Name = $"Transport Fee - Student {assignment.StudentId} - Route {route.Name}",
                AcademicYearId = assignment.AcademicYearId,
                TermId = assignment.TermId,
                StudentId = assignment.StudentId,
                Status = "active",
                Currency = route.Currency,
                IsMandatory = false,
                CreatedBy = userId
            };
            _db.Set<Fees.Entities.FeeStructure>().Add(existingStructure);
            await _db.SaveChangesAsync(ct);
        }

        // Add or update fee structure item for transport
        var existingItem = await _db.Set<Fees.Entities.FeeStructureItem>().FirstOrDefaultAsync(fsi => fsi.TenantId == tenantId && fsi.FeeStructureId == existingStructure.Id && fsi.FeeItemId == feeItem.Id && !f.IsDeleted, ct);
        if (existingItem == null)
        {
            existingItem = new Fees.Entities.FeeStructureItem
            {
                TenantId = tenantId,
                FeeStructureId = existingStructure.Id,
                FeeItemId = feeItem.Id,
                Description = $"Transport - {route.Name} - Pickup {assignment.PickupStopId}",
                Amount = route.FeeAmount,
                Currency = route.Currency,
                Quantity = 1,
                LineTotal = route.FeeAmount,
                CreatedBy = userId
            };
            _db.Set<Fees.Entities.FeeStructureItem>().Add(existingItem);
        }
        else
        {
            existingItem.Amount = route.FeeAmount;
            existingItem.LineTotal = route.FeeAmount;
            existingItem.Description = $"Transport - {route.Name} - Pickup {assignment.PickupStopId}";
        }

        assignment.FeeStructureItemId = existingItem.Id;
        assignment.FeeApplied = true;

        // Create link record
        var link = new TransportFeeLink
        {
            TenantId = tenantId,
            TransportAssignmentId = assignment.Id,
            FeeItemId = feeItem.Id,
            FeeStructureId = existingStructure.Id,
            FeeStructureItemId = existingItem.Id,
            Amount = route.FeeAmount,
            Currency = route.Currency,
            CreatedBy = userId
        };
        _db.Set<TransportFeeLink>().Add(link);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<TransportAttendanceDto>> MarkBoardingAsync(long tenantId, long userId, MarkBoardingRequest req, CancellationToken ct = default)
    {
        var result = new List<TransportAttendanceDto>();
        foreach (var item in req.Items)
        {
            var assignment = await _db.Set<TransportAssignment>().FirstOrDefaultAsync(a => a.TenantId == tenantId && a.StudentId == item.StudentId && a.RouteId == req.RouteId && a.Status == "active" && !a.IsDeleted, ct);
            if (assignment == null) continue;

            var existing = await _db.Set<TransportAttendance>().FirstOrDefaultAsync(ta => ta.TenantId == tenantId && ta.RouteId == req.RouteId && ta.StudentId == item.StudentId && ta.TripDate == req.TripDate.Date && ta.TripType == req.TripType && !ta.IsDeleted, ct);
            if (existing != null)
            {
                existing.Status = item.Status;
                existing.Notes = item.Notes;
                if (!string.IsNullOrEmpty(item.ActualBoardingTime) && TimeSpan.TryParse(item.ActualBoardingTime, out var t)) existing.ActualBoardingTime = t;
                existing.UpdatedBy = userId;

                var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == item.StudentId, ct);
                result.Add(new TransportAttendanceDto(existing.Id, existing.RouteId, existing.RouteStopId, existing.StudentId, student != null ? $"{student.FirstName} {student.LastName}" : "", existing.TripDate, existing.TripType, existing.Status, existing.ActualBoardingTime?.ToString(@"hh\:mm"), existing.Notes));

                // If absent, notify guardians using existing messaging module
                if (item.Status == "absent" || item.Status == "missed")
                {
                    await NotifyAbsenceAsync(tenantId, userId, req.RouteId, item.StudentId, req.TripDate, ct);
                }
            }
            else
            {
                var attendance = new TransportAttendance
                {
                    TenantId = tenantId,
                    RouteId = req.RouteId,
                    RouteStopId = req.RouteStopId,
                    StudentId = item.StudentId,
                    TransportAssignmentId = assignment.Id,
                    TripDate = req.TripDate.Date,
                    TripType = req.TripType,
                    Status = item.Status,
                    Notes = item.Notes,
                    ActualBoardingTime = !string.IsNullOrEmpty(item.ActualBoardingTime) && TimeSpan.TryParse(item.ActualBoardingTime, out var t) ? t : null,
                    MarkedByUserId = userId,
                    CreatedBy = userId
                };
                _db.Set<TransportAttendance>().Add(attendance);
                await _db.SaveChangesAsync(ct);

                var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == item.StudentId, ct);
                result.Add(new TransportAttendanceDto(attendance.Id, attendance.RouteId, attendance.RouteStopId, attendance.StudentId, student != null ? $"{student.FirstName} {student.LastName}" : "", attendance.TripDate, attendance.TripType, attendance.Status, attendance.ActualBoardingTime?.ToString(@"hh\:mm"), attendance.Notes));

                if (item.Status == "absent" || item.Status == "missed")
                {
                    await NotifyAbsenceAsync(tenantId, userId, req.RouteId, item.StudentId, req.TripDate, ct);
                }
            }
        }
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<List<TransportAttendanceDto>> GetBoardingAttendanceAsync(long tenantId, long routeId, DateTime tripDate, string? tripType, CancellationToken ct = default)
    {
        var query = _db.Set<TransportAttendance>().Where(a => a.TenantId == tenantId && a.RouteId == routeId && a.TripDate == tripDate.Date && !a.IsDeleted);
        if (!string.IsNullOrEmpty(tripType)) query = query.Where(a => a.TripType == tripType);
        var list = await query.ToListAsync(ct);
        var result = new List<TransportAttendanceDto>();
        foreach (var a in list)
        {
            var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == a.StudentId, ct);
            result.Add(new TransportAttendanceDto(a.Id, a.RouteId, a.RouteStopId, a.StudentId, student != null ? $"{student.FirstName} {student.LastName}" : "", a.TripDate, a.TripType, a.Status, a.ActualBoardingTime?.ToString(@"hh\:mm"), a.Notes));
        }
        return result;
    }

    public async Task<List<UtilisationReportDto>> GetUtilisationReportAsync(long tenantId, CancellationToken ct = default)
    {
        // PERFORMANCE FIX: Was N+1 - 2 CountAsync per route
        var routes = await _db.Set<Route>().Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Include(r => r.Vehicle)
            .Include(r => r.Stops)
            .AsNoTracking()
            .ToListAsync(ct);

        if (routes.Count == 0) return new List<UtilisationReportDto>();

        var routeIds = routes.Select(r => r.Id).ToList();
        var today = DateTime.UtcNow.Date;

        var assignedCounts = await _db.Set<TransportAssignment>()
            .Where(a => a.TenantId == tenantId && routeIds.Contains(a.RouteId) && a.Status == "active" && !a.IsDeleted)
            .GroupBy(a => a.RouteId)
            .Select(g => new { RouteId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RouteId, x => x.Count, ct);

        var boardedTodayCounts = await _db.Set<TransportAttendance>()
            .Where(a => a.TenantId == tenantId && routeIds.Contains(a.RouteId) && a.TripDate == today && a.Status == "boarded" && !a.IsDeleted)
            .GroupBy(a => a.RouteId)
            .Select(g => new { RouteId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RouteId, x => x.Count, ct);

        var result = new List<UtilisationReportDto>(routes.Count);
        foreach (var route in routes)
        {
            assignedCounts.TryGetValue(route.Id, out var assigned);
            boardedTodayCounts.TryGetValue(route.Id, out var boardedToday);
            var capacity = route.Vehicle?.Capacity ?? 0;
            var utilisation = capacity > 0 ? Math.Round((decimal)assigned / capacity * 100, 1) : 0m;
            var boardingRate = assigned > 0 ? Math.Round((decimal)boardedToday / assigned * 100, 1) : 0m;
            result.Add(new UtilisationReportDto(route.Id, route.Name, capacity, assigned, utilisation, boardedToday, boardingRate, route.Stops.Count));
        }
        return result;
    }

    public async Task<List<RevenueReportDto>> GetRevenueReportAsync(long tenantId, long academicYearId, long termId, CancellationToken ct = default)
    {
        var routes = await _db.Set<Route>().Where(r => r.TenantId == tenantId && !r.IsDeleted).ToListAsync(ct);
        var result = new List<RevenueReportDto>();
        foreach (var route in routes)
        {
            var assignments = await _db.Set<TransportAssignment>().Where(a => a.TenantId == tenantId && a.RouteId == route.Id && a.AcademicYearId == academicYearId && a.TermId == termId && !a.IsDeleted).ToListAsync(ct);
            var assignedCount = assignments.Count;
            var feePerLearner = route.FeeAmount;
            var totalInvoiced = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.AcademicYearId == academicYearId && i.TermId == termId && assignments.Select(a => a.StudentId).Contains(i.StudentId) && !i.IsDeleted).SumAsync(i => i.TotalAmount, ct);
            var totalCollected = await _db.Set<Fees.Entities.FeeInvoice>().Where(i => i.TenantId == tenantId && i.AcademicYearId == academicYearId && i.TermId == termId && assignments.Select(a => a.StudentId).Contains(i.StudentId) && !i.IsDeleted).SumAsync(i => i.AmountPaid, ct);
            var totalArrears = totalInvoiced - totalCollected;
            var collectionRate = totalInvoiced > 0 ? Math.Round(totalCollected / totalInvoiced * 100, 1) : 0m;

            result.Add(new RevenueReportDto(route.Id, route.Name, feePerLearner, assignedCount, totalInvoiced, totalCollected, totalArrears, collectionRate, route.Currency));
        }
        return result;
    }

    public async Task<UnassignedLearnersDto> GetUnassignedLearnersAsync(long tenantId, long? gradeId, CancellationToken ct = default)
    {
        var allStudents = _db.Set<Student>().Where(s => s.TenantId == tenantId && !s.IsDeleted);
        if (gradeId.HasValue) allStudents = allStudents.Where(s => s.GradeId == gradeId.Value);

        var assignedStudentIds = await _db.Set<TransportAssignment>().Where(a => a.TenantId == tenantId && a.Status == "active" && !a.IsDeleted).Select(a => a.StudentId).Distinct().ToListAsync(ct);

        var unassigned = await allStudents.Where(s => !assignedStudentIds.Contains(s.Id)).ToListAsync(ct);

        var result = new List<UnassignedStudentDto>();
        foreach (var s in unassigned)
        {
            var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == s.GradeId, ct);
            var stream = await _db.Streams.FirstOrDefaultAsync(st => st.Id == s.StreamId, ct);
            result.Add(new UnassignedStudentDto(s.Id, $"{s.FirstName} {s.LastName}", s.StudentNumber, grade?.Name ?? "", stream?.Name ?? "", null));
        }

        return new UnassignedLearnersDto(result, result.Count);
    }

    public async Task<RouteManifestDto> GetRouteManifestAsync(long tenantId, long routeId, CancellationToken ct = default)
    {
        var route = await _db.Set<Route>().Where(r => r.Id == routeId && r.TenantId == tenantId && !r.IsDeleted).Include(r => r.Vehicle).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Route not found");
        var vehicle = route.VehicleId.HasValue ? await _db.Set<Vehicle>().FirstOrDefaultAsync(v => v.Id == route.VehicleId.Value, ct) : null;
        var driver = route.DriverId.HasValue ? await _db.Set<Driver>().FirstOrDefaultAsync(d => d.Id == route.DriverId.Value, ct) : null;
        var assistant = route.AssistantId.HasValue ? await _db.Set<Driver>().FirstOrDefaultAsync(d => d.Id == route.AssistantId.Value, ct) : null;

        var stops = await _db.Set<RouteStop>().Where(s => s.TenantId == tenantId && s.RouteId == routeId && !s.IsDeleted).OrderBy(s => s.OrderNumber).ToListAsync(ct);
        var assignments = await _db.Set<TransportAssignment>().Where(a => a.TenantId == tenantId && a.RouteId == routeId && a.Status == "active" && !a.IsDeleted).ToListAsync(ct);

        var stopManifests = new List<StopManifestDto>();
        foreach (var stop in stops)
        {
            var learnersAtStop = assignments.Where(a => a.PickupStopId == stop.Id).ToList();
            var learnerManifests = new List<LearnerManifestDto>();
            foreach (var assignment in learnersAtStop)
            {
                var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == assignment.StudentId, ct);
                if (student == null) continue;
                var grade = await _db.Grades.FirstOrDefaultAsync(g => g.Id == student.GradeId, ct);
                var stream = await _db.Streams.FirstOrDefaultAsync(s => s.Id == student.StreamId, ct);

                // Guardian contact for manifest
                var guardianLink = await _db.Set<GuardianStudentLink>().FirstOrDefaultAsync(l => l.TenantId == tenantId && l.StudentId == student.Id && l.IsPrimaryContact && !l.IsDeleted, ct);
                var guardian = guardianLink != null ? await _db.Set<Guardian>().FirstOrDefaultAsync(g => g.Id == guardianLink.GuardianId, ct) : null;
                var dropStop = assignment.DropStopId.HasValue ? stops.FirstOrDefault(s => s.Id == assignment.DropStopId.Value) : null;

                learnerManifests.Add(new LearnerManifestDto(
                    student.Id,
                    $"{student.FirstName} {student.LastName}",
                    student.StudentNumber,
                    grade?.Name ?? "",
                    stream?.Name ?? "",
                    guardian?.Phone,
                    guardian != null ? $"{guardian.FirstName} {guardian.LastName}" : null,
                    stop.Name,
                    dropStop?.Name
                ));
            }

            stopManifests.Add(new StopManifestDto(
                stop.Id,
                stop.Name,
                stop.Address,
                stop.OrderNumber,
                stop.ExpectedArrivalTime?.ToString(@"hh\:mm"),
                learnersAtStop.Count,
                learnerManifests
            ));
        }

        var vehicleDto = vehicle != null ? new VehicleDto(vehicle.Id, vehicle.RegistrationNumber, vehicle.Make, vehicle.Model, vehicle.Capacity, vehicle.Year, vehicle.FuelType, vehicle.InsuranceExpiry, vehicle.LicenceExpiry, vehicle.FitnessExpiry, vehicle.ServiceDueDate, vehicle.IsActive, vehicle.Status, 0, 0, false, false) : null;
        var driverDto = driver != null ? new DriverDto(driver.Id, driver.FullName, driver.Role, driver.StaffId, driver.LicenceNumber, driver.LicenceType, driver.LicenceExpiry, driver.MedicalExpiry, driver.Phone, driver.Email, driver.IsActive, 0, false) : null;
        var assistantDto = assistant != null ? new DriverDto(assistant.Id, assistant.FullName, assistant.Role, assistant.StaffId, assistant.LicenceNumber, assistant.LicenceType, assistant.LicenceExpiry, assistant.MedicalExpiry, assistant.Phone, assistant.Email, assistant.IsActive, 0, false) : null;

        return new RouteManifestDto(route.Id, route.Name, route.Code, route.Direction, vehicleDto, driverDto, assistantDto, stopManifests, DateTime.UtcNow);
    }

    public async Task NotifyRouteChangeAsync(long tenantId, long userId, long routeId, string changeDescription, CancellationToken ct = default)
    {
        // Route change notifications to guardians using existing messaging module
        var route = await _db.Set<Route>().FirstOrDefaultAsync(r => r.Id == routeId && r.TenantId == tenantId && !r.IsDeleted, ct);
        if (route == null) return;

        var assignments = await _db.Set<TransportAssignment>().Where(a => a.TenantId == tenantId && a.RouteId == routeId && a.Status == "active" && !a.IsDeleted).ToListAsync(ct);

        var messageBody = $"Transport Update: Route {route.Name} - {changeDescription} - Please check new stop times. - {route.Name}";

        var log = new TransportNotificationLog
        {
            TenantId = tenantId,
            RouteId = routeId,
            NotificationType = "route_change",
            Title = $"Route {route.Name} Updated",
            Body = messageBody,
            RecipientsJson = System.Text.Json.JsonSerializer.Serialize(assignments.Select(a => a.StudentId).ToList()),
            SentAt = DateTime.UtcNow,
            SentByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<TransportNotificationLog>().Add(log);
        await _db.SaveChangesAsync(ct);

        // In real app, create MessageBatch via messaging module: audience class/stream? Actually guardians of learners assigned to route
        // For V1, we log and would call messaging service to send SMS/email to guardians using existing messaging module
        // Example: await _messagingService.SendToGuardiansOfStudents(tenantId, assignments.Select(a=>a.StudentId).ToList(), messageBody, channel sms/email)
    }

    public async Task NotifyAbsenceAsync(long tenantId, long userId, long routeId, long studentId, DateTime tripDate, CancellationToken ct = default)
    {
        var route = await _db.Set<Route>().FirstOrDefaultAsync(r => r.Id == routeId && r.TenantId == tenantId && !r.IsDeleted, ct);
        if (route == null) return;

        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId && s.TenantId == tenantId, ct);
        var messageBody = $"Transport Absence: {student?.FirstName} {student?.LastName} was marked absent/missed for {route.Name} on {tripDate:dd/MM/yyyy}. Please contact school if reason not provided.";

        var log = new TransportNotificationLog
        {
            TenantId = tenantId,
            RouteId = routeId,
            StudentId = studentId,
            NotificationType = "absence",
            Title = $"Transport Absence - {student?.FirstName}",
            Body = messageBody,
            SentAt = DateTime.UtcNow,
            SentByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<TransportNotificationLog>().Add(log);
        await _db.SaveChangesAsync(ct);

        // Would call existing messaging module to send to guardians of this learner
    }

    private async Task<TransportAssignmentDto> MapAssignmentAsync(TransportAssignment a, CancellationToken ct)
    {
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == a.StudentId, ct);
        var route = await _db.Set<Route>().FirstOrDefaultAsync(r => r.Id == a.RouteId, ct);
        var pickupStop = await _db.Set<RouteStop>().FirstOrDefaultAsync(s => s.Id == a.PickupStopId, ct);
        var dropStop = a.DropStopId.HasValue ? await _db.Set<RouteStop>().FirstOrDefaultAsync(s => s.Id == a.DropStopId.Value, ct) : null;
        var grade = student != null ? await _db.Grades.FirstOrDefaultAsync(g => g.Id == student.GradeId, ct) : null;
        var stream = student != null ? await _db.Streams.FirstOrDefaultAsync(s => s.Id == student.StreamId, ct) : null;

        return new TransportAssignmentDto(
            a.Id,
            a.StudentId,
            student != null ? $"{student.FirstName} {student.LastName}" : $"Student {a.StudentId}",
            student?.StudentNumber ?? "",
            grade?.Name ?? "",
            stream?.Name ?? "",
            a.RouteId,
            route?.Name ?? "",
            a.PickupStopId,
            pickupStop?.Name ?? "",
            a.DropStopId,
            dropStop?.Name,
            a.AssignedDate,
            a.Status,
            a.AcademicYearId,
            a.TermId,
            route?.FeeAmount ?? 0m,
            route?.Currency ?? "USD",
            a.FeeApplied
        );
    }

    
// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade/Stream/Guardian - single source of truth
