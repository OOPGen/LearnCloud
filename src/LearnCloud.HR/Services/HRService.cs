using LearnCloud.HR.DTOs;
using LearnCloud.HR.Entities;
using LearnCloud.MultiTenancy.Context;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.HR.Services;

public interface IHRService
{
    Task<StaffDto> CreateStaffAsync(long tenantId, long userId, CreateStaffRequest req, CancellationToken ct = default);
    Task<List<StaffDto>> GetStaffAsync(long tenantId, CancellationToken ct = default);
    Task<ContractDto> CreateContractAsync(long tenantId, long userId, CreateContractRequest req, CancellationToken ct = default);
    Task<List<ContractDto>> GetExpiringContractsAsync(long tenantId, int daysAhead, CancellationToken ct = default);
    Task<LeaveRequestDto> CreateLeaveRequestAsync(long tenantId, long userId, CreateLeaveRequestDto req, CancellationToken ct = default);
    Task<LeaveRequestDto> ApproveLeaveAsync(long tenantId, long userId, long leaveRequestId, ApproveLeaveRequest req, CancellationToken ct = default);
    Task<LeaveBalanceDto> GetLeaveBalanceAsync(long tenantId, long staffId, int academicYear, CancellationToken ct = default);
    Task<List<LeaveCalendarDto>> GetLeaveCalendarAsync(long tenantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<AppraisalCycleDto> CreateAppraisalCycleAsync(long tenantId, long userId, CreateAppraisalCycleRequest req, CancellationToken ct = default);
    Task<DisciplinaryRecordDto> CreateDisciplinaryAsync(long tenantId, long userId, CreateDisciplinaryRequest req, CancellationToken ct = default);
    Task<HeadcountReportDto> GetHeadcountReportAsync(long tenantId, CancellationToken ct = default);
    Task<PayrollExportResultDto> ExportPayrollReadyAsync(long tenantId, long userId, PayrollExportRequest req, CancellationToken ct = default);
}

public class HRService : IHRService
{
    private readonly LearnCloudDbContext _db;

    public HRService(LearnCloudDbContext db) => _db = db;

    public async Task<StaffDto> CreateStaffAsync(long tenantId, long userId, CreateStaffRequest req, CancellationToken ct = default)
    {
        var staffNumber = $"STA-{DateTime.UtcNow.Year}-{(await _db.Set<Staff>().CountAsync(s => s.TenantId == tenantId, ct) + 1):D5}";
        var staff = new Staff
        {
            TenantId = tenantId,
            StaffNumber = staffNumber,
            FirstName = req.FirstName,
            LastName = req.LastName,
            NationalId = req.NationalId,
            DateOfBirth = req.DateOfBirth,
            Gender = req.Gender,
            EmploymentType = req.EmploymentType,
            DepartmentId = req.DepartmentId,
            Designation = req.Designation,
            HireDate = req.HireDate,
            Phone = req.Phone,
            Email = req.Email,
            CurrentSalary = req.CurrentSalary,
            Currency = req.Currency,
            UserId = req.UserId,
            CreatedBy = userId
        };
        _db.Set<Staff>().Add(staff);
        await _db.SaveChangesAsync(ct);

        return new StaffDto(staff.Id, staff.StaffNumber, staff.FirstName, staff.LastName, $"{staff.FirstName} {staff.LastName}", staff.NationalId, staff.DateOfBirth, staff.Gender, staff.EmploymentType, staff.EmploymentStatus, staff.DepartmentId, null, staff.Designation, staff.HireDate, staff.Phone, staff.Email, staff.PhotoUrl, staff.CurrentSalary, staff.Currency, staff.UserId);
    }

    public async Task<List<StaffDto>> GetStaffAsync(long tenantId, CancellationToken ct = default)
    {
        var list = await _db.Set<Staff>().Where(s => s.TenantId == tenantId && !s.IsDeleted).Include(s => s.Department).ToListAsync(ct);
        return list.Select(s => new StaffDto(s.Id, s.StaffNumber, s.FirstName, s.LastName, $"{s.FirstName} {s.LastName}", s.NationalId, s.DateOfBirth, s.Gender, s.EmploymentType, s.EmploymentStatus, s.DepartmentId, s.Department?.Name, s.Designation, s.HireDate, s.Phone, s.Email, s.PhotoUrl, s.CurrentSalary, s.Currency, s.UserId)).ToList();
    }

    public async Task<ContractDto> CreateContractAsync(long tenantId, long userId, CreateContractRequest req, CancellationToken ct = default)
    {
        var contractNumber = $"CONT-{DateTime.UtcNow.Year}-{(await _db.Set<Contract>().CountAsync(c => c.TenantId == tenantId, ct) + 1):D5}";
        var contract = new Contract
        {
            TenantId = tenantId,
            StaffId = req.StaffId,
            ContractNumber = contractNumber,
            ContractType = req.ContractType,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            ProbationEndDate = req.ProbationEndDate,
            Salary = req.Salary,
            Currency = req.Currency,
            Terms = req.Terms,
            Status = "active",
            CreatedByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<Contract>().Add(contract);
        await _db.SaveChangesAsync(ct);

        var daysToExpiry = (contract.EndDate - DateTime.UtcNow).Days;
        return new ContractDto(contract.Id, contract.StaffId, contract.ContractNumber, contract.ContractType, contract.StartDate, contract.EndDate, contract.ProbationEndDate, contract.Salary, contract.Currency, contract.Status, contract.Terms, daysToExpiry, daysToExpiry <= 30);
    }

    public async Task<List<ContractDto>> GetExpiringContractsAsync(long tenantId, int daysAhead, CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow.AddDays(daysAhead);
        var contracts = await _db.Set<Contract>().Where(c => c.TenantId == tenantId && c.EndDate <= threshold && c.Status == "active" && !c.IsDeleted).ToListAsync(ct);
        return contracts.Select(c => new ContractDto(c.Id, c.StaffId, c.ContractNumber, c.ContractType, c.StartDate, c.EndDate, c.ProbationEndDate, c.Salary, c.Currency, c.Status, c.Terms, (c.EndDate - DateTime.UtcNow).Days, true)).ToList();
    }

    public async Task<LeaveRequestDto> CreateLeaveRequestAsync(long tenantId, long userId, CreateLeaveRequestDto req, CancellationToken ct = default)
    {
        // Calculate days requested excluding weekends (simplified - should use school calendar)
        var days = CalculateWorkingDays(req.StartDate, req.EndDate, req.IsHalfDay);
        
        // Check balance
        var entitlement = await _db.Set<LeaveEntitlement>().FirstOrDefaultAsync(e => e.TenantId == tenantId && e.StaffId == req.StaffId && e.LeaveTypeId == req.LeaveTypeId && e.AcademicYear == req.StartDate.Year && !e.IsDeleted, ct);
        if (entitlement != null && entitlement.RemainingDays < days)
            throw new InvalidOperationException($"Insufficient leave balance: remaining {entitlement.RemainingDays}, requested {days}");

        var leave = new LeaveRequest
        {
            TenantId = tenantId,
            StaffId = req.StaffId,
            LeaveTypeId = req.LeaveTypeId,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            DaysRequested = days,
            Reason = req.Reason,
            Status = "pending",
            IsHalfDay = req.IsHalfDay,
            DocumentUrl = req.DocumentUrl,
            RequestedByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<LeaveRequest>().Add(leave);
        await _db.SaveChangesAsync(ct);

        var staff = await _db.Set<Staff>().FirstOrDefaultAsync(s => s.Id == req.StaffId, ct);
        var leaveType = await _db.Set<LeaveType>().FirstOrDefaultAsync(lt => lt.Id == req.LeaveTypeId, ct);

        return new LeaveRequestDto(leave.Id, leave.StaffId, staff != null ? $"{staff.FirstName} {staff.LastName}" : "", leave.LeaveTypeId, leaveType?.Name ?? "", leave.StartDate, leave.EndDate, leave.DaysRequested, leave.Reason, leave.Status, leave.ApproverUserId, null, leave.DocumentUrl, leave.IsHalfDay, leave.CreatedAt);
    }

    public async Task<LeaveRequestDto> ApproveLeaveAsync(long tenantId, long userId, long leaveRequestId, ApproveLeaveRequest req, CancellationToken ct = default)
    {
        var leave = await _db.Set<LeaveRequest>().FirstOrDefaultAsync(l => l.Id == leaveRequestId && l.TenantId == tenantId && !l.IsDeleted, ct) ?? throw new InvalidOperationException("Leave request not found");

        leave.Status = req.Status;
        leave.ApproverUserId = userId;
        leave.ApprovedAt = DateTime.UtcNow;
        leave.UpdatedBy = userId;

        // Update entitlement used days
        if (req.Status == "approved")
        {
            var entitlement = await _db.Set<LeaveEntitlement>().FirstOrDefaultAsync(e => e.TenantId == tenantId && e.StaffId == leave.StaffId && e.LeaveTypeId == leave.LeaveTypeId && e.AcademicYear == leave.StartDate.Year && !e.IsDeleted, ct);
            if (entitlement != null)
            {
                entitlement.UsedDays += leave.DaysRequested;
                entitlement.RemainingDays = entitlement.EntitledDays + entitlement.CarriedForwardDays - entitlement.UsedDays;
            }
        }

        await _db.SaveChangesAsync(ct);

        var staff = await _db.Set<Staff>().FirstOrDefaultAsync(s => s.Id == leave.StaffId, ct);
        var leaveType = await _db.Set<LeaveType>().FirstOrDefaultAsync(lt => lt.Id == leave.LeaveTypeId, ct);

        return new LeaveRequestDto(leave.Id, leave.StaffId, staff != null ? $"{staff.FirstName} {staff.LastName}" : "", leave.LeaveTypeId, leaveType?.Name ?? "", leave.StartDate, leave.EndDate, leave.DaysRequested, leave.Reason, leave.Status, leave.ApproverUserId, req.ApproverComment, leave.DocumentUrl, leave.IsHalfDay, leave.CreatedAt);
    }

    public async Task<LeaveBalanceDto> GetLeaveBalanceAsync(long tenantId, long staffId, int academicYear, CancellationToken ct = default)
    {
        var entitlements = await _db.Set<LeaveEntitlement>().Where(e => e.TenantId == tenantId && e.StaffId == staffId && e.AcademicYear == academicYear && !e.IsDeleted).Include(e => e.LeaveType).ToListAsync(ct);
        var staff = await _db.Set<Staff>().FirstOrDefaultAsync(s => s.Id == staffId, ct);

        var dtos = entitlements.Select(e => new LeaveEntitlementDto(e.Id, e.StaffId, staff != null ? $"{staff.FirstName} {staff.LastName}" : "", e.LeaveTypeId, e.LeaveType.Name, e.AcademicYear, e.EntitledDays, e.CarriedForwardDays, e.UsedDays, e.RemainingDays, e.ExpiryDate)).ToList();

        return new LeaveBalanceDto(staffId, staff != null ? $"{staff.FirstName} {staff.LastName}" : "", dtos, dtos.Sum(d => d.EntitledDays), dtos.Sum(d => d.UsedDays), dtos.Sum(d => d.RemainingDays));
    }

    public async Task<List<LeaveCalendarDto>> GetLeaveCalendarAsync(long tenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var leaves = await _db.Set<LeaveRequest>().Where(l => l.TenantId == tenantId && l.Status == "approved" && l.StartDate <= to && l.EndDate >= from && !l.IsDeleted).Include(l => l.LeaveType).ToListAsync(ct);

        var calendar = new List<LeaveCalendarDto>();
        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
        {
            var leavesOnDate = leaves.Where(l => l.StartDate.Date <= date && l.EndDate.Date >= date).ToList();
            var dtos = new List<LeaveRequestDto>();
            foreach (var l in leavesOnDate)
            {
                var staff = await _db.Set<Staff>().FirstOrDefaultAsync(s => s.Id == l.StaffId, ct);
                var lt = await _db.Set<LeaveType>().FirstOrDefaultAsync(t => t.Id == l.LeaveTypeId, ct);
                dtos.Add(new LeaveRequestDto(l.Id, l.StaffId, staff != null ? $"{staff.FirstName} {staff.LastName}" : "", l.LeaveTypeId, lt?.Name ?? "", l.StartDate, l.EndDate, l.DaysRequested, l.Reason, l.Status, l.ApproverUserId, null, l.DocumentUrl, l.IsHalfDay, l.CreatedAt));
            }
            calendar.Add(new LeaveCalendarDto(date, dtos, dtos.Count));
        }

        return calendar;
    }

    public async Task<AppraisalCycleDto> CreateAppraisalCycleAsync(long tenantId, long userId, CreateAppraisalCycleRequest req, CancellationToken ct = default)
    {
        var cycle = new AppraisalCycle
        {
            TenantId = tenantId,
            Name = req.Name,
            AcademicYearId = req.AcademicYearId,
            TermId = req.TermId,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Description = req.Description,
            Status = "draft",
            CreatedBy = userId
        };
        _db.Set<AppraisalCycle>().Add(cycle);
        await _db.SaveChangesAsync(ct);

        foreach (var crit in req.Criteria)
        {
            var criterion = new AppraisalCriterion
            {
                TenantId = tenantId,
                AppraisalCycleId = cycle.Id,
                Name = crit.Name,
                Description = crit.Description,
                Weight = crit.Weight,
                MaxScore = crit.MaxScore,
                SortOrder = crit.SortOrder,
                CreatedBy = userId
            };
            _db.Set<AppraisalCriterion>().Add(criterion);
        }
        await _db.SaveChangesAsync(ct);

        var criteria = await _db.Set<AppraisalCriterion>().Where(c => c.TenantId == tenantId && c.AppraisalCycleId == cycle.Id && !c.IsDeleted).ToListAsync(ct); // SECURITY C5
        return new AppraisalCycleDto(cycle.Id, cycle.Name, cycle.AcademicYearId, cycle.TermId, cycle.StartDate, cycle.EndDate, cycle.Status, cycle.Description, criteria.Select(c => new AppraisalCriterionDto(c.Id, c.AppraisalCycleId, c.Name, c.Description, c.Weight, c.MaxScore, c.SortOrder)).ToList());
    }

    public async Task<DisciplinaryRecordDto> CreateDisciplinaryAsync(long tenantId, long userId, CreateDisciplinaryRequest req, CancellationToken ct = default)
    {
        var record = new DisciplinaryRecord
        {
            TenantId = tenantId,
            StaffId = req.StaffId,
            IncidentDate = req.IncidentDate,
            IncidentType = req.IncidentType,
            Title = req.Title,
            Description = req.Description,
            Severity = req.Severity,
            ActionTaken = req.ActionTaken,
            Status = "open",
            ReportedByUserId = userId,
            Visibility = req.Visibility,
            IsConfidential = req.IsConfidential,
            CreatedBy = userId
        };
        _db.Set<DisciplinaryRecord>().Add(record);
        await _db.SaveChangesAsync(ct);

        var staff = await _db.Set<Staff>().FirstOrDefaultAsync(s => s.Id == req.StaffId, ct);
        return new DisciplinaryRecordDto(record.Id, record.StaffId, staff != null ? $"{staff.FirstName} {staff.LastName}" : "", record.IncidentDate, record.IncidentType, record.Title, record.Description, record.Severity, record.ActionTaken, record.Status, record.ReportedByUserId, record.Visibility, record.IsConfidential, null);
    }

    public async Task<HeadcountReportDto> GetHeadcountReportAsync(long tenantId, CancellationToken ct = default)
    {
        var active = await _db.Set<Staff>().CountAsync(s => s.TenantId == tenantId && s.EmploymentStatus == "active" && !s.IsDeleted, ct);
        var onLeave = await _db.Set<Staff>().CountAsync(s => s.TenantId == tenantId && s.EmploymentStatus == "on_leave" && !s.IsDeleted, ct);
        var terminated = await _db.Set<Staff>().CountAsync(s => s.TenantId == tenantId && s.EmploymentStatus == "terminated" && !s.IsDeleted, ct);

        var byDept = await _db.Set<Staff>().Where(s => s.TenantId == tenantId && s.EmploymentStatus == "active" && !s.IsDeleted).GroupBy(s => s.DepartmentId).Select(g => new { deptId = g.Key, count = g.Count() }).ToListAsync(ct);
        var byDeptDict = new Dictionary<string, int>();
        foreach (var item in byDept)
        {
            var deptName = item.deptId.HasValue ? (await _db.Set<Department>().FirstOrDefaultAsync(d => d.Id == item.deptId.Value, ct))?.Name ?? "Unknown" : "No Department";
            byDeptDict[deptName] = item.count;
        }

        var byType = await _db.Set<Staff>().Where(s => s.TenantId == tenantId && s.EmploymentStatus == "active" && !s.IsDeleted).GroupBy(s => s.EmploymentType).Select(g => new { type = g.Key, count = g.Count() }).ToListAsync(ct);
        var byTypeDict = byType.ToDictionary(x => x.type, x => x.count);

        // Monthly trend last 12 months
        var monthlyTrend = new List<MonthlyHeadcountDto>();
        for (int i = 11; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddMonths(-i);
            var year = date.Year;
            var month = date.Month;
            var headcount = await _db.Set<Staff>().CountAsync(s => s.TenantId == tenantId && s.HireDate <= new DateTime(year, month, DateTime.DaysInMonth(year, month)) && !s.IsDeleted, ct);
            // Simplified joined/left
            monthlyTrend.Add(new MonthlyHeadcountDto(year, month, headcount, 0, 0));
        }

        return new HeadcountReportDto(active, onLeave, terminated, byDeptDict, byTypeDict, monthlyTrend);
    }

    public async Task<PayrollExportResultDto> ExportPayrollReadyAsync(long tenantId, long userId, PayrollExportRequest req, CancellationToken ct = default)
    {
        var staffQuery = _db.Set<Staff>().Where(s => s.TenantId == tenantId && s.EmploymentStatus == "active" && !s.IsDeleted);
        if (req.StaffIds != null && req.StaffIds.Any())
            staffQuery = staffQuery.Where(s => req.StaffIds.Contains(s.Id));

        var staffList = await staffQuery.ToListAsync(ct);
        var departmentIds = staffList.Where(s => s.DepartmentId.HasValue).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var departments = await _db.Set<Department>().Where(d => d.TenantId == tenantId && departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        // Payroll-ready file for import into Belina/Pastel. LearnCloud holds only the basic
        // salary: allowances, overtime, leave, deductions and bank details are left blank for
        // the payroll product. The export used to invent fixed allowances (100/50/30) for every
        // employee and write 0 into the bank columns. Values go through CsvWriter, which
        // quotes them and neutralises spreadsheet formulas.
        var csvLines = new List<string>
        {
            Infrastructure.Text.CsvWriter.Row("EmployeeCode", "NationalID", "FullName", "Department", "Designation", "EmploymentType", "HireDate", "BasicSalary", "Currency",
                "Allowances_Housing", "Allowances_Transport", "Allowances_COLA", "OvertimeHours", "OvertimeRate", "LeaveDaysTakenAnnual", "LeaveDaysTakenSick", "UnpaidLeaveDays",
                "Deductions_Loans", "Deductions_Union", "BankName", "AccountNumber", "Branch", "Notes"),
        };

        decimal totalGross = 0m;
        foreach (var staff in staffList)
        {
            var basic = staff.CurrentSalary ?? 0m;
            totalGross += basic;
            var department = staff.DepartmentId is long d ? departments.GetValueOrDefault(d) : null;
            csvLines.Add(Infrastructure.Text.CsvWriter.Row(
                staff.StaffNumber, staff.NationalId, $"{staff.FirstName} {staff.LastName}", department, staff.Designation, staff.EmploymentType,
                staff.HireDate.ToString("yyyy-MM-dd"), basic.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), staff.Currency,
                null, null, null, null, null, null, null, null, null, null, null, null, null,
                staff.CurrentSalary is null ? "No salary recorded in LearnCloud" : "PAYE/NSSA/ZIMDEF to be calculated by the payroll product"));
        }

        var fileName = $"payroll_ready_{req.Year}_{req.Month}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var fileContent = System.Text.Encoding.UTF8.GetString(Infrastructure.Text.CsvWriter.ToUtf8(csvLines));
        // SECURITY FIX C6: Store in secure location outside wwwroot with tenantId and random GUID, not predictable
        var randomFileName = $"{Guid.NewGuid():N}_{fileName}";
        var exportsDir = Path.Combine(AppContext.BaseDirectory, "exports", "payroll", tenantId.ToString());
        Directory.CreateDirectory(exportsDir);
        var fullPath = Path.Combine(exportsDir, randomFileName);
        await File.WriteAllTextAsync(fullPath, fileContent, ct);
        
        var fileUrl = $"/api/files/payroll/{tenantId}/{randomFileName}"; // Secure URL with tenantId check via FilesController

        return new PayrollExportResultDto(randomFileName, fileUrl, staffList.Count, totalGross, "USD", DateTime.UtcNow, userId, req.Format);
    }

    private static int CalculateWorkingDays(DateTime start, DateTime end, bool isHalfDay)
    {
        if (isHalfDay) return 1; // simplified half day = 0.5? For demo 1
        var days = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                days++;
        }
        return days;
    }
}
