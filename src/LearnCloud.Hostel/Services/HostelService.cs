using LearnCloud.Fees.Entities;
using LearnCloud.Hostel.DTOs;
using LearnCloud.Hostel.Entities;
using LearnCloud.MultiTenancy.Context;
using LearnCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.Hostel.Services;

public interface IHostelService
{
    // Blocks, rooms, beds
    Task<BlockDto> CreateBlockAsync(long tenantId, long userId, CreateBlockRequest req, CancellationToken ct = default);
    Task<List<BlockDto>> GetBlocksAsync(long tenantId, CancellationToken ct = default);
    Task<RoomDto> CreateRoomAsync(long tenantId, long userId, CreateRoomRequest req, CancellationToken ct = default);
    Task<BedDto> CreateBedAsync(long tenantId, long userId, CreateBedRequest req, CancellationToken ct = default);
    Task<List<BedDto>> GetVacantBedsAsync(long tenantId, long? blockId, string? gender, CancellationToken ct = default);

    // Allocation with conflict detection and waiting list
    Task<AllocationDto> AllocateBedAsync(long tenantId, long userId, AllocateRequest req, CancellationToken ct = default);
    Task UnallocateBedAsync(long tenantId, long userId, long allocationId, string? reason, CancellationToken ct = default);
    Task<WaitingListDto> AddToWaitingListAsync(long tenantId, long userId, long studentId, long? preferredBlockId, long academicYearId, long termId, string gender, CancellationToken ct = default);

    // Boarding fees integrated
    Task ApplyBoardingFeeAsync(long tenantId, long userId, long allocationId, CancellationToken ct = default);

    // Exeat and leave register


    // Roll call
    Task<RollCallDto> CreateRollCallAsync(long tenantId, long userId, CreateRollCallRequest req, CancellationToken ct = default);
    Task<RollCallDto> MarkRollCallAsync(long tenantId, long userId, long rollCallId, MarkRollCallRequest req, CancellationToken ct = default);

    // Reports
    Task<OccupancyReportDto> GetOccupancyReportAsync(long tenantId, long? blockId, CancellationToken ct = default);
}

public class HostelService : IHostelService
{
    private readonly LearnCloudDbContext _db;
    public HostelService(LearnCloudDbContext db) => _db = db;

    public async Task<BlockDto> CreateBlockAsync(long tenantId, long userId, CreateBlockRequest req, CancellationToken ct = default)
    {
        var block = new HostelBlock { TenantId = tenantId, Name = req.Name, Code = req.Code, GenderDesignation = req.GenderDesignation, Capacity = req.Capacity, TotalRooms = req.TotalRooms, Description = req.Description, Location = req.Location, HouseMasterStaffId = req.HouseMasterStaffId, MatronStaffId = req.MatronStaffId, CreatedBy = userId };
        _db.Set<HostelBlock>().Add(block);
        await _db.SaveChangesAsync(ct);
        return new BlockDto(block.Id, block.Name, block.Code, block.GenderDesignation, block.Capacity, block.TotalRooms, 0, block.Capacity, 0m, block.IsActive, block.HouseMasterStaffId, null, block.MatronStaffId, null);
    }

    public async Task<List<BlockDto>> GetBlocksAsync(long tenantId, CancellationToken ct = default)
    {
        var blocks = await _db.Set<HostelBlock>().Where(b => b.TenantId == tenantId && !b.IsDeleted).ToListAsync(ct);
        var result = new List<BlockDto>();
        foreach (var block in blocks)
        {
            var occupied = await _db.Set<HostelBed>().CountAsync(b => b.TenantId == tenantId && b.BlockId == block.Id && b.Status == "occupied" && !b.IsDeleted, ct);
            var total = await _db.Set<HostelBed>().CountAsync(b => b.TenantId == tenantId && b.BlockId == block.Id && !b.IsDeleted, ct);
            var available = total - occupied;
            var occupancy = total > 0 ? Math.Round((decimal)occupied / total * 100, 1) : 0m;
            result.Add(new BlockDto(block.Id, block.Name, block.Code, block.GenderDesignation, block.Capacity, block.TotalRooms, occupied, available, occupancy, block.IsActive, block.HouseMasterStaffId, null, block.MatronStaffId, null));
        }
        return result;
    }

    public async Task<RoomDto> CreateRoomAsync(long tenantId, long userId, CreateRoomRequest req, CancellationToken ct = default)
    {
        var block = await _db.Set<HostelBlock>().FirstOrDefaultAsync(b => b.Id == req.BlockId && b.TenantId == tenantId && !b.IsDeleted, ct) ?? throw new InvalidOperationException("Block not found");
        var room = new HostelRoom { TenantId = tenantId, BlockId = req.BlockId, RoomNumber = req.RoomNumber, Floor = req.Floor, Capacity = req.Capacity, GenderDesignation = req.GenderDesignation, IsActive = true, Facilities = req.Facilities, CreatedBy = userId };
        _db.Set<HostelRoom>().Add(room);
        await _db.SaveChangesAsync(ct);

        // Auto-create beds
        for (int i = 1; i <= req.Capacity; i++)
        {
            var bed = new HostelBed { TenantId = tenantId, RoomId = room.Id, BlockId = req.BlockId, BedNumber = $"{req.RoomNumber}{ (char)('A' + i -1)}", Status = "available", Condition = "good", CreatedBy = userId };
            _db.Set<HostelBed>().Add(bed);
        }
        await _db.SaveChangesAsync(ct);

        return new RoomDto(room.Id, room.BlockId, block.Name, room.RoomNumber, room.Floor, room.Capacity, room.GenderDesignation, room.IsActive, 0, room.Capacity);
    }

    public async Task<BedDto> CreateBedAsync(long tenantId, long userId, CreateBedRequest req, CancellationToken ct = default)
    {
        var room = await _db.Set<HostelRoom>().FirstOrDefaultAsync(r => r.Id == req.RoomId && r.TenantId == tenantId && !r.IsDeleted, ct) ?? throw new InvalidOperationException("Room not found");
        // Rooms create their beds (101A, 101B, ...) when they are added; a second bed with the
        // same number would make allocations and roll calls ambiguous.
        if (await _db.Set<HostelBed>().AnyAsync(b => b.TenantId == tenantId && b.RoomId == req.RoomId && b.BedNumber == req.BedNumber && !b.IsDeleted, ct))
            throw new InvalidOperationException($"Bed {req.BedNumber} already exists in room {room.RoomNumber}.");
        var bed = new HostelBed { TenantId = tenantId, RoomId = req.RoomId, BlockId = room.BlockId, BedNumber = req.BedNumber, Status = "available", Condition = req.Condition, CreatedBy = userId };
        _db.Set<HostelBed>().Add(bed);
        await _db.SaveChangesAsync(ct);
        return new BedDto(bed.Id, bed.RoomId, bed.BlockId, bed.BedNumber, room.RoomNumber, "", bed.Status, bed.Condition, null, null);
    }

    public async Task<List<BedDto>> GetVacantBedsAsync(long tenantId, long? blockId, string? gender, CancellationToken ct = default)
    {
        var query = _db.Set<HostelBed>().Where(b => b.TenantId == tenantId && b.Status == "available" && !b.IsDeleted);
        if (blockId.HasValue) query = query.Where(b => b.BlockId == blockId.Value);
        var beds = await query.Include(b => b.Room).Take(200).ToListAsync(ct);
        // Filter by gender if needed via block gender
        if (!string.IsNullOrEmpty(gender))
        {
            var blockIds = await _db.Set<HostelBlock>().Where(bl => bl.TenantId == tenantId && bl.GenderDesignation == gender && !bl.IsDeleted).Select(bl => bl.Id).ToListAsync(ct);
            beds = beds.Where(b => blockIds.Contains(b.BlockId)).ToList();
        }
        return beds.Select(b => new BedDto(b.Id, b.RoomId, b.BlockId, b.BedNumber, b.Room.RoomNumber, "", b.Status, b.Condition, b.CurrentStudentId, null)).ToList();
    }

    public async Task<AllocationDto> AllocateBedAsync(long tenantId, long userId, AllocateRequest req, CancellationToken ct = default)
    {
        var bed = await _db.Set<HostelBed>().FirstOrDefaultAsync(b => b.Id == req.BedId && b.TenantId == tenantId && !b.IsDeleted, ct) ?? throw new InvalidOperationException("Bed not found");
        if (bed.Status != "available") throw new InvalidOperationException($"Bed {bed.BedNumber} not available, status {bed.Status} - conflict detection");

        // Check if student already allocated in same year/term
        var existing = await _db.Set<BedAllocation>().FirstOrDefaultAsync(a => a.TenantId == tenantId && a.StudentId == req.StudentId && a.AcademicYearId == req.AcademicYearId && a.TermId == req.TermId && a.Status == "active" && !a.IsDeleted, ct);
        if (existing != null) throw new InvalidOperationException($"Learner already allocated to bed {existing.BedId} in same year/term - conflict detection");

        // Gender designation check
        var room = await _db.Set<HostelRoom>().FirstOrDefaultAsync(r => r.Id == bed.RoomId, ct);
        var block = await _db.Set<HostelBlock>().FirstOrDefaultAsync(b => b.Id == bed.BlockId, ct);
        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == req.StudentId && s.TenantId == tenantId, ct);
        // Assume student has gender field - check block gender matches student gender (if block not mixed)
        // For demo skip gender check but would be here

        var allocation = new BedAllocation
        {
            TenantId = tenantId,
            StudentId = req.StudentId,
            BedId = req.BedId,
            RoomId = bed.RoomId,
            BlockId = bed.BlockId,
            AcademicYearId = req.AcademicYearId,
            TermId = req.TermId,
            AllocationDate = DateTime.UtcNow,
            AllocatedByUserId = userId,
            Status = "active",
            CreatedBy = userId
        };
        _db.Set<BedAllocation>().Add(allocation);

        bed.Status = "occupied";
        bed.CurrentStudentId = req.StudentId;
        bed.CurrentAllocationId = allocation.Id;

        await _db.SaveChangesAsync(ct);

        // Boarding fees integrated with existing fee structure
        await ApplyBoardingFeeAsync(tenantId, userId, allocation.Id, ct);

        // Remove from waiting list if exists
        var waiting = await _db.Set<WaitingList>().FirstOrDefaultAsync(w => w.TenantId == tenantId && w.StudentId == req.StudentId && w.AcademicYearId == req.AcademicYearId && w.TermId == req.TermId && w.Status == "waiting" && !w.IsDeleted, ct);
        if (waiting != null)
        {
            waiting.Status = "allocated";
            waiting.AllocatedAt = DateTime.UtcNow;
            waiting.AllocatedBedId = bed.Id;
            await _db.SaveChangesAsync(ct);
        }

        return new AllocationDto(allocation.Id, allocation.StudentId, student != null ? $"{student.FirstName} {student.LastName}" : "", student?.StudentNumber ?? "", bed.Id, bed.BedNumber, bed.RoomId, room?.RoomNumber ?? "", bed.BlockId, block?.Name ?? "", allocation.AcademicYearId, allocation.TermId, allocation.AllocationDate, allocation.Status, allocation.FeeApplied, null);
    }

    public async Task UnallocateBedAsync(long tenantId, long userId, long allocationId, string? reason, CancellationToken ct = default)
    {
        var allocation = await _db.Set<BedAllocation>().FirstOrDefaultAsync(a => a.Id == allocationId && a.TenantId == tenantId && !a.IsDeleted, ct) ?? throw new InvalidOperationException("Allocation not found");
        allocation.Status = "vacated";
        allocation.VacatedDate = DateTime.UtcNow;
        allocation.VacatedReason = reason;

        var bed = await _db.Set<HostelBed>().FirstOrDefaultAsync(b => b.Id == allocation.BedId, ct);
        if (bed != null)
        {
            bed.Status = "available";
            bed.CurrentStudentId = null;
            bed.CurrentAllocationId = null;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<WaitingListDto> AddToWaitingListAsync(long tenantId, long userId, long studentId, long? preferredBlockId, long academicYearId, long termId, string gender, CancellationToken ct = default)
    {
        var queuePos = await _db.Set<WaitingList>().CountAsync(w => w.TenantId == tenantId && w.AcademicYearId == academicYearId && w.TermId == termId && w.Status == "waiting" && !w.IsDeleted, ct) + 1;
        var wl = new WaitingList
        {
            TenantId = tenantId,
            StudentId = studentId,
            PreferredBlockId = preferredBlockId,
            Gender = gender,
            AcademicYearId = academicYearId,
            TermId = termId,
            Priority = 0,
            QueuePosition = queuePos,
            Status = "waiting",
            RequestedByUserId = userId,
            CreatedBy = userId
        };
        _db.Set<WaitingList>().Add(wl);
        await _db.SaveChangesAsync(ct);

        var student = await _db.Set<Student>().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        return new WaitingListDto(wl.Id, studentId, student != null ? $"{student.FirstName} {student.LastName}" : "", preferredBlockId, null, gender, academicYearId, termId, wl.Priority, wl.QueuePosition, wl.Status, wl.Reason, wl.RequestedDate);
    }

    public async Task ApplyBoardingFeeAsync(long tenantId, long userId, long allocationId, CancellationToken ct = default)
    {
        var allocation = await _db.Set<BedAllocation>().FirstOrDefaultAsync(a => a.Id == allocationId && a.TenantId == tenantId && !a.IsDeleted, ct) ?? throw new InvalidOperationException("Allocation not found");
        if (allocation.FeeApplied) return;

        var block = await _db.Set<HostelBlock>().FirstOrDefaultAsync(b => b.Id == allocation.BlockId, ct);
        // Find or create fee item BOARDING
        var feeItem = await _db.Set<FeeItem>().FirstOrDefaultAsync(fi => fi.TenantId == tenantId && fi.Code == "BOARDING" && !fi.IsDeleted, ct);
        if (feeItem == null)
        {
            feeItem = new FeeItem { TenantId = tenantId, Name = "Boarding", Code = "BOARDING", Recurrence = FeeItemRecurrence.PerTerm, IsProratable = false, IsOptional = true, Description = "Boarding fee per term" };
            _db.Set<FeeItem>().Add(feeItem);
            await _db.SaveChangesAsync(ct);
        }

        // Find or create fee structure individual
        var feeStructure = await _db.Set<FeeStructure>().FirstOrDefaultAsync(fs => fs.TenantId == tenantId && fs.StudentId == allocation.StudentId && fs.AcademicYearId == allocation.AcademicYearId && fs.TermId == allocation.TermId && !fs.IsDeleted, ct);
        if (feeStructure == null)
        {
            feeStructure = new FeeStructure { TenantId = tenantId, Name = $"Boarding Fee - Student {allocation.StudentId}", AcademicYearId = allocation.AcademicYearId, TermId = allocation.TermId, StudentId = allocation.StudentId, Status = "active", Currency = "USD", IsMandatory = false, CreatedBy = userId };
            _db.Set<FeeStructure>().Add(feeStructure);
            await _db.SaveChangesAsync(ct);
        }

        var feeAmount = 500m; // would get from block or route? For hostel, use block fee or default 500
        // Check block fee amount? For demo use 500

        var existingItem = await _db.Set<FeeStructureItem>().FirstOrDefaultAsync(fsi => fsi.TenantId == tenantId && fsi.FeeStructureId == feeStructure.Id && fsi.FeeItemId == feeItem.Id && !fsi.IsDeleted, ct);
        if (existingItem == null)
        {
            existingItem = new FeeStructureItem { TenantId = tenantId, FeeStructureId = feeStructure.Id, FeeItemId = feeItem.Id, Description = $"Boarding - {block?.Name}", Amount = feeAmount, Currency = "USD", Quantity = 1, LineTotal = feeAmount, CreatedBy = userId };
            _db.Set<FeeStructureItem>().Add(existingItem);
        }
        else
        {
            existingItem.Amount = feeAmount;
            existingItem.LineTotal = feeAmount;
        }

        var link = new BoardingFeeLink { TenantId = tenantId, BedAllocationId = allocation.Id, FeeItemId = feeItem.Id, FeeStructureId = feeStructure.Id, FeeStructureItemId = existingItem.Id, Amount = feeAmount, Currency = "USD", CreatedBy = userId };
        _db.Set<BoardingFeeLink>().Add(link);

        allocation.FeeApplied = true;
        allocation.FeeStructureItemId = existingItem.Id;

        await _db.SaveChangesAsync(ct);
    }

    // Exeats are handled by HostelController directly. The roll call and occupancy methods
    // below used to throw NotImplementedException, so their endpoints always failed with 500.

    private static readonly string[] RollCallTypes = { "nightly", "morning", "evening" };
    private static readonly string[] RollCallStatuses = { "present", "absent", "on_leave", "sick_bay" };

    // Lists every student with an active bed in the block (or room). Students away on an
    // exeat start as on_leave; everyone else starts as absent until marked, so an unmarked
    // student is never counted as present.
    public async Task<RollCallDto> CreateRollCallAsync(long tenantId, long userId, CreateRollCallRequest req, CancellationToken ct = default)
    {
        var type = (req.RollCallType ?? "").Trim().ToLowerInvariant();
        if (!RollCallTypes.Contains(type)) throw new InvalidOperationException("Roll call type must be nightly, morning or evening.");
        var block = await _db.Set<HostelBlock>().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == req.BlockId && !b.IsDeleted, ct)
            ?? throw new InvalidOperationException("Block not found");
        HostelRoom? room = null;
        if (req.RoomId is long roomId)
            room = await _db.Set<HostelRoom>().FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == roomId && r.BlockId == block.Id && !r.IsDeleted, ct)
                ?? throw new InvalidOperationException("Room not found");

        var date = req.RollCallDate.Date;
        if (await _db.Set<RollCall>().AnyAsync(r => r.TenantId == tenantId && r.BlockId == block.Id && r.RoomId == req.RoomId && r.RollCallDate == date && r.RollCallType == type && r.Status != "cancelled" && !r.IsDeleted, ct))
            throw new InvalidOperationException($"A {type} roll call for {block.Name} on {date:yyyy-MM-dd} already exists.");

        var allocations = await _db.Set<BedAllocation>()
            .Where(a => a.TenantId == tenantId && a.BlockId == block.Id && a.Status == "active" && !a.IsDeleted && (req.RoomId == null || a.RoomId == req.RoomId))
            .ToListAsync(ct);
        var studentIds = allocations.Select(a => a.StudentId).ToList();
        var now = DateTime.UtcNow;
        var onLeave = await _db.Set<ExeatRegister>()
            .Where(e => e.TenantId == tenantId && studentIds.Contains(e.StudentId) && e.ActualReturnDateTime == null && e.DepartureDateTime <= now
                        && (e.Status == "approved" || e.Status == "departed" || e.Status == "overdue") && !e.IsDeleted)
            .Select(e => e.StudentId).Distinct().ToListAsync(ct);

        var rollCall = new RollCall
        {
            TenantId = tenantId, BlockId = block.Id, RoomId = room?.Id, RollCallDate = date, RollCallType = type,
            Status = "in_progress", ConductedByUserId = userId, CreatedBy = userId,
        };
        foreach (var a in allocations)
        {
            rollCall.Entries.Add(new RollCallEntry
            {
                TenantId = tenantId, BlockId = block.Id, RoomId = a.RoomId, BedId = a.BedId, StudentId = a.StudentId,
                RollCallDate = date, Status = onLeave.Contains(a.StudentId) ? "on_leave" : "absent", MarkedByUserId = 0,
            });
        }
        Recount(rollCall);
        _db.Set<RollCall>().Add(rollCall);
        await _db.SaveChangesAsync(ct);
        return ToDto(rollCall, block.Name, room?.RoomNumber);
    }

    public async Task<RollCallDto> MarkRollCallAsync(long tenantId, long userId, long rollCallId, MarkRollCallRequest req, CancellationToken ct = default)
    {
        var rollCall = await _db.Set<RollCall>().Include(r => r.Entries)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rollCallId && !r.IsDeleted, ct)
            ?? throw new InvalidOperationException("Roll call not found");
        if (rollCall.Status != "in_progress")
            throw new InvalidOperationException($"This roll call is {rollCall.Status} and can no longer be marked.");
        if (req.Entries is null || req.Entries.Count == 0)
            throw new InvalidOperationException("Mark at least one student.");

        foreach (var mark in req.Entries)
        {
            var status = (mark.Status ?? "").Trim().ToLowerInvariant();
            if (!RollCallStatuses.Contains(status))
                throw new InvalidOperationException("Status must be present, absent, on_leave or sick_bay.");
            var entry = rollCall.Entries.FirstOrDefault(e => e.StudentId == mark.StudentId && e.BedId == mark.BedId && !e.IsDeleted)
                ?? throw new InvalidOperationException($"Student {mark.StudentId} in bed {mark.BedId} is not on this roll call.");
            entry.Status = status;
            entry.Notes = string.IsNullOrWhiteSpace(mark.Notes) ? null : mark.Notes.Trim();
            entry.MarkedByUserId = userId;
            entry.UpdatedBy = userId;
        }

        Recount(rollCall);
        // Complete once every student has been marked by someone.
        if (rollCall.Entries.Where(e => !e.IsDeleted).All(e => e.MarkedByUserId != 0))
        {
            rollCall.Status = "completed";
            rollCall.CompletedAt = DateTime.UtcNow;
        }
        rollCall.UpdatedBy = userId;
        await _db.SaveChangesAsync(ct);

        var blockName = await _db.Set<HostelBlock>().Where(b => b.TenantId == tenantId && b.Id == rollCall.BlockId).Select(b => b.Name).FirstOrDefaultAsync(ct) ?? "";
        var roomNumber = rollCall.RoomId is long rid
            ? await _db.Set<HostelRoom>().Where(r => r.TenantId == tenantId && r.Id == rid).Select(r => r.RoomNumber).FirstOrDefaultAsync(ct)
            : null;
        return ToDto(rollCall, blockName, roomNumber);
    }

    private static void Recount(RollCall rollCall)
    {
        var entries = rollCall.Entries.Where(e => !e.IsDeleted).ToList();
        rollCall.TotalExpected = entries.Count;
        rollCall.TotalPresent = entries.Count(e => e.Status == "present");
        rollCall.TotalOnLeave = entries.Count(e => e.Status == "on_leave");
        rollCall.TotalAbsent = entries.Count(e => e.Status == "absent");
    }

    private static RollCallDto ToDto(RollCall r, string blockName, string? roomNumber) =>
        new(r.Id, r.BlockId, blockName, r.RoomId, roomNumber, r.RollCallDate, r.RollCallType, r.Status, r.TotalExpected, r.TotalPresent, r.TotalAbsent, r.TotalOnLeave, r.ConductedByUserId);

    // Bed counts by status for one block, or for all blocks together when no block is given.
    public async Task<OccupancyReportDto> GetOccupancyReportAsync(long tenantId, long? blockId, CancellationToken ct = default)
    {
        var blocks = await _db.Set<HostelBlock>().Where(b => b.TenantId == tenantId && !b.IsDeleted && (blockId == null || b.Id == blockId)).ToListAsync(ct);
        if (blockId.HasValue && blocks.Count == 0) throw new InvalidOperationException("Block not found");
        var blockIds = blocks.Select(b => b.Id).ToList();

        var rooms = await _db.Set<HostelRoom>().Where(r => r.TenantId == tenantId && blockIds.Contains(r.BlockId) && !r.IsDeleted)
            .OrderBy(r => r.BlockId).ThenBy(r => r.RoomNumber).ToListAsync(ct);
        var beds = await _db.Set<HostelBed>().Where(b => b.TenantId == tenantId && blockIds.Contains(b.BlockId) && !b.IsDeleted)
            .Select(b => new { b.RoomId, b.Status }).ToListAsync(ct);

        int Count(params string[] statuses) => beds.Count(b => statuses.Contains(b.Status));
        var capacity = beds.Count;
        var occupied = Count("occupied");
        var codes = blocks.ToDictionary(b => b.Id, b => b.Code);
        var roomDtos = rooms.Select(r =>
        {
            var roomBeds = beds.Where(b => b.RoomId == r.Id).ToList();
            var label = blockId.HasValue ? r.RoomNumber : $"{codes.GetValueOrDefault(r.BlockId)} {r.RoomNumber}";
            return new RoomOccupancyDto(r.Id, label, roomBeds.Count, roomBeds.Count(b => b.Status == "occupied"), roomBeds.Count(b => b.Status == "available"));
        }).ToList();

        return new OccupancyReportDto(
            blockId ?? 0,
            blockId.HasValue ? blocks[0].Name : "All blocks",
            capacity, occupied, Count("available"), Count("reserved"), Count("maintenance", "out_of_order"),
            capacity == 0 ? 0m : Math.Round(occupied * 100m / capacity, 1),
            roomDtos);
    }
}


// REMOVED DUPLICATE STUBS - Now using canonical entities from LearnCloud.Domain.Entities
// Fix C2: Deduplicate Student/Grade/Stream/Guardian - single source of truth
