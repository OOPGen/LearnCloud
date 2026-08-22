using LearnCloud.Transport.DTOs;
using LearnCloud.Transport.Services;
using LearnCloud.MultiTenancy.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace LearnCloud.Transport.Controllers;

[ApiController]
[Route("api/transport")]
[Authorize]
[EnableRateLimiting("api_general")] // SECURITY FIX: Rate limiting 60/m per user/IP - prevents DoS
public class TransportController : ControllerBase
{
    private readonly ITransportService _transport;
    private readonly ITenantContext _tenantContext;

    public TransportController(ITransportService transport, ITenantContext tenantContext)
    {
        _transport = transport;
        _tenantContext = tenantContext;
    }

    private long TenantId => _tenantContext.TenantId ?? throw new InvalidOperationException("No tenant");
    private long UserId => _tenantContext.ActorUserId ?? long.Parse(User.FindFirst("uid")?.Value ?? "0");

    // Routes with ordered stops and expected times
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("routes")]
    [Authorize(Roles = "SCHOOL_ADMIN,TRANSPORT_MANAGER")]
    public async Task<IActionResult> CreateRoute([FromBody] CreateRouteRequest req, CancellationToken ct)
    {
        var route = await _transport.CreateRouteAsync(TenantId, UserId, req, ct);
        return Ok(route);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("routes")]
    public async Task<IActionResult> GetRoutes(CancellationToken ct)
    {
        var routes = await _transport.GetRoutesAsync(TenantId, ct);
        return Ok(routes);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("routes/{routeId:long}")]
    public async Task<IActionResult> GetRoute(long routeId, CancellationToken ct)
    {
        var route = await _transport.GetRouteAsync(TenantId, routeId, ct);
        return Ok(route);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("routes/{routeId:long}/stops")]
    public async Task<IActionResult> AddStop(long routeId, [FromBody] CreateRouteStopRequest req, CancellationToken ct)
    {
        var stop = await _transport.AddStopAsync(TenantId, UserId, routeId, req, ct);
        return Ok(stop);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("routes/{routeId:long}/stops")]
    public async Task<IActionResult> GetStops(long routeId, CancellationToken ct)
    {
        var stops = await _transport.GetStopsAsync(TenantId, routeId, ct);
        return Ok(stops);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("routes/{routeId:long}/stops/reorder")]
    public async Task<IActionResult> ReorderStops(long routeId, [FromBody] ReorderStopsRequest req, CancellationToken ct)
    {
        await _transport.ReorderStopsAsync(TenantId, routeId, req, ct);
        return Ok(new { message = "Stops reordered, route change notification sent via messaging module" });
    }

    // Vehicles with capacity, registration, insurance and licence expiry with reminders
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("vehicles")]
    public async Task<IActionResult> CreateVehicle([FromBody] CreateVehicleRequest req, CancellationToken ct)
    {
        var vehicle = await _transport.CreateVehicleAsync(TenantId, UserId, req, ct);
        return Ok(vehicle);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("vehicles")]
    public async Task<IActionResult> GetVehicles(CancellationToken ct)
    {
        var vehicles = await _transport.GetVehiclesAsync(TenantId, ct);
        return Ok(vehicles);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("vehicles/needing-reminder")]
    public async Task<IActionResult> GetVehiclesNeedingReminder(CancellationToken ct)
    {
        var vehicles = await _transport.GetVehiclesNeedingReminderAsync(TenantId, ct);
        return Ok(vehicles);
    }

    // Drivers and assistants with licence expiry tracking
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("drivers")]
    public async Task<IActionResult> CreateDriver([FromBody] CreateDriverRequest req, CancellationToken ct)
    {
        var driver = await _transport.CreateDriverAsync(TenantId, UserId, req, ct);
        return Ok(driver);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("drivers")]
    public async Task<IActionResult> GetDrivers([FromQuery] string? role, CancellationToken ct)
    {
        var drivers = await _transport.GetDriversAsync(TenantId, role, ct);
        return Ok(drivers);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("drivers/needing-licence-reminder")]
    public async Task<IActionResult> GetDriversNeedingLicenceReminder(CancellationToken ct)
    {
        var drivers = await _transport.GetDriversNeedingLicenceReminderAsync(TenantId, ct);
        return Ok(drivers);
    }

    // Learner assignment to a route and stop with capacity enforcement
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("assignments")]
    public async Task<IActionResult> AssignLearner([FromBody] AssignLearnerRequest req, CancellationToken ct)
    {
        var assignment = await _transport.AssignLearnerAsync(TenantId, UserId, req, ct);
        return Ok(assignment);
    }

    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("assignments/bulk")]
    public async Task<IActionResult> BulkAssign([FromBody] BulkAssignRequest req, CancellationToken ct)
    {
        var assignments = await _transport.BulkAssignAsync(TenantId, UserId, req, ct);
        return Ok(assignments);
    }

    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [HttpDelete("assignments/{assignmentId:long}")]
    public async Task<IActionResult> UnassignLearner(long assignmentId, [FromBody] UnassignLearnerRequest req, CancellationToken ct)
    {
        await _transport.UnassignLearnerAsync(TenantId, UserId, assignmentId, req, ct);
        return Ok(new { message = "Unassigned, fee not automatically removed - manual credit note if needed, notification sent" });
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("routes/{routeId:long}/assignments")]
    public async Task<IActionResult> GetAssignmentsByRoute(long routeId, CancellationToken ct)
    {
        var assignments = await _transport.GetAssignmentsByRouteAsync(TenantId, routeId, ct);
        return Ok(assignments);
    }

    // Boarding attendance per trip if school wants it
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [HttpPost("attendance/boarding")]
    public async Task<IActionResult> MarkBoarding([FromBody] MarkBoardingRequest req, CancellationToken ct)
    {
        var attendance = await _transport.MarkBoardingAsync(TenantId, UserId, req, ct);
        return Ok(attendance);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("attendance/boarding")]
    public async Task<IActionResult> GetBoardingAttendance([FromQuery] long routeId, [FromQuery] DateTime tripDate, [FromQuery] string? tripType, CancellationToken ct)
    {
        var attendance = await _transport.GetBoardingAttendanceAsync(TenantId, routeId, tripDate, tripType, ct);
        return Ok(attendance);
    }

    // Reports on utilisation per route, revenue per route, and unassigned learners
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/utilisation")]
    public async Task<IActionResult> GetUtilisationReport(CancellationToken ct)
    {
        var report = await _transport.GetUtilisationReportAsync(TenantId, ct);
        return Ok(report);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/revenue")]
    public async Task<IActionResult> GetRevenueReport([FromQuery] long academicYearId, [FromQuery] long termId, CancellationToken ct)
    {
        var report = await _transport.GetRevenueReportAsync(TenantId, academicYearId, termId, ct);
        return Ok(report);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("reports/unassigned-learners")]
    public async Task<IActionResult> GetUnassignedLearners([FromQuery] long? gradeId, CancellationToken ct)
    {
        var report = await _transport.GetUnassignedLearnersAsync(TenantId, gradeId, ct);
        return Ok(report);
    }

    // Printable route manifest for each driver
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("routes/{routeId:long}/manifest")]
    public async Task<IActionResult> GetRouteManifest(long routeId, CancellationToken ct)
    {
        var manifest = await _transport.GetRouteManifestAsync(TenantId, routeId, ct);
        return Ok(manifest);
    }

    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [HttpGet("routes/{routeId:long}/manifest/print")]
    public async Task<IActionResult> GetRouteManifestPrint(long routeId, CancellationToken ct)
    {
        var manifest = await _transport.GetRouteManifestAsync(TenantId, routeId, ct);
        var html = $@"
<html><head><style>
body{{font-family:sans-serif; font-size:12px}}
table{{border-collapse:collapse;width:100%}}
th,td{{border:1px solid black;padding:6px}}
@media print{{body{{margin:0}}}}
</style></head><body>
<h1>Route Manifest - {manifest.RouteName} ({manifest.Code}) - {manifest.Direction}</h1>
<p>Vehicle: {manifest.Vehicle?.RegistrationNumber} {manifest.Vehicle?.Make} {manifest.Vehicle?.Model} Capacity {manifest.Vehicle?.Capacity} | Driver: {manifest.Driver?.FullName} {manifest.Driver?.Phone} | Assistant: {manifest.Assistant?.FullName}</p>
<p>Generated: {manifest.GeneratedAt:dd/MM/yyyy HH:mm}</p>
<table><tr><th>Order</th><th>Stop Name</th><th>Address</th><th>Expected Arrival</th><th>Learners</th></tr>
{string.Join("", manifest.Stops.Select(s => $"<tr><td>{s.OrderNumber}</td><td>{s.StopName}</td><td>{s.Address}</td><td>{s.ExpectedArrivalTime}</td><td>{s.LearnersCount}<br/>{string.Join("<br/>", s.Learners.Select(l => $"{l.StudentName} ({l.StudentNumber}) {l.GradeName} {l.StreamName} - Guardian {l.GuardianPhone}"))}</td></tr>"))}
</table>
<p>Printed from LearnCloud Transport Module - Bulawayo</p>
</body></html>";
        return Content(html, "text/html");
    }
}