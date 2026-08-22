using LearnCloud.AttendanceTimetable.DTOs;
using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.MultiTenancy.Context;
using Microsoft.EntityFrameworkCore;

namespace LearnCloud.AttendanceTimetable.Services;

public interface ITimetableService
{
    Task<List<PeriodDefinitionDto>> GetPeriodsAsync(long tenantId, long? academicYearId, CancellationToken ct = default);
    Task<PeriodDefinitionDto> CreatePeriodAsync(long tenantId, long userId, CreatePeriodRequest req, CancellationToken ct = default);
    Task BulkSavePeriodsAsync(long tenantId, long userId, BulkPeriodsRequest req, CancellationToken ct = default);

    Task<TimetableDto> CreateTimetableAsync(long tenantId, long userId, CreateTimetableRequest req, CancellationToken ct = default);
    Task<TimetableDto> GetTimetableAsync(long tenantId, long timetableId, CancellationToken ct = default);
    Task<List<TimetableDto>> ListTimetablesAsync(long tenantId, long academicYearId, long termId, CancellationToken ct = default);

    Task<ClashResponseDto> CheckClashAsync(long tenantId, long timetableId, CreateSlotRequest newSlot, long? excludeSlotId = null, CancellationToken ct = default);
    Task<TimetableSlotDto> CreateSlotAsync(long tenantId, long userId, long timetableId, CreateSlotRequest req, CancellationToken ct = default);
    Task<ClashResponseDto> BulkCreateSlotsAsync(long tenantId, long userId, long timetableId, BulkSlotsRequest req, CancellationToken ct = default);
    Task DeleteSlotAsync(long tenantId, long timetableId, long slotId, CancellationToken ct = default);

    Task<TimetableGridDto> GetGridAsync(long tenantId, TimetableViewRequest req, CancellationToken ct = default);
    Task<TimetableGridDto> GetByClassAsync(long tenantId, long gradeId, long streamId, long academicYearId, long termId, DateTime? effectiveDate, CancellationToken ct = default);
    Task<TimetableGridDto> GetByTeacherAsync(long tenantId, long teacherStaffId, long academicYearId, long termId, DateTime? effectiveDate, CancellationToken ct = default);
}

public class TimetableService : ITimetableService
{
    private readonly LearnCloudDbContext _db;

    public TimetableService(LearnCloudDbContext db) => _db = db;

    // Periods
    public async Task<List<PeriodDefinitionDto>> GetPeriodsAsync(long tenantId, long? academicYearId, CancellationToken ct = default)
    {
        var periods = await _db.Set<PeriodDefinition>()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && (academicYearId == null || p.AcademicYearId == academicYearId || p.AcademicYearId == null))
            .OrderBy(p => p.SortOrder).ToListAsync(ct);

        if (!periods.Any())
        {
            // Sensible defaults: 8 periods + breaks
            var defaults = new List<PeriodDefinition>
            {
                new(){TenantId=tenantId, PeriodNumber=1, Name="Period 1", StartTime=new TimeSpan(8,0,0), EndTime=new TimeSpan(8,45,0), IsBreak=false, SortOrder=1},
                new(){TenantId=tenantId, PeriodNumber=2, Name="Period 2", StartTime=new TimeSpan(8,45,0), EndTime=new TimeSpan(9,30,0), IsBreak=false, SortOrder=2},
                new(){TenantId=tenantId, PeriodNumber=3, Name="Break", StartTime=new TimeSpan(9,30,0), EndTime=new TimeSpan(10,0,0), IsBreak=true, SortOrder=3},
                new(){TenantId=tenantId, PeriodNumber=4, Name="Period 3", StartTime=new TimeSpan(10,0,0), EndTime=new TimeSpan(10,45,0), IsBreak=false, SortOrder=4},
                new(){TenantId=tenantId, PeriodNumber=5, Name="Period 4", StartTime=new TimeSpan(10,45,0), EndTime=new TimeSpan(11,30,0), IsBreak=false, SortOrder=5},
                new(){TenantId=tenantId, PeriodNumber=6, Name="Lunch", StartTime=new TimeSpan(11,30,0), EndTime=new TimeSpan(12,30,0), IsBreak=true, SortOrder=6},
                new(){TenantId=tenantId, PeriodNumber=7, Name="Period 5", StartTime=new TimeSpan(12,30,0), EndTime=new TimeSpan(13,15,0), IsBreak=false, SortOrder=7},
                new(){TenantId=tenantId, PeriodNumber=8, Name="Period 6", StartTime=new TimeSpan(13,15,0), EndTime=new TimeSpan(14,0,0), IsBreak=false, SortOrder=8},
            };
            _db.Set<PeriodDefinition>().AddRange(defaults);
            await _db.SaveChangesAsync(ct);
            periods = defaults;
        }

        return periods.Select(p => new PeriodDefinitionDto(p.PeriodNumber, p.Name, p.StartTime.ToString(@"hh\:mm"), p.EndTime.ToString(@"hh\:mm"), p.IsBreak, p.SortOrder)).ToList();
    }

    public async Task<PeriodDefinitionDto> CreatePeriodAsync(long tenantId, long userId, CreatePeriodRequest req, CancellationToken ct = default)
    {
        var entity = new PeriodDefinition
        {
            TenantId = tenantId,
            Name = req.Name,
            StartTime = TimeSpan.Parse(req.StartTime),
            EndTime = TimeSpan.Parse(req.EndTime),
            IsBreak = req.IsBreak,
            SortOrder = req.SortOrder,
            AcademicYearId = req.AcademicYearId,
            CreatedBy = userId,
            PeriodNumber = await _db.Set<PeriodDefinition>().Where(p=>p.TenantId==tenantId).CountAsync(ct) + 1
        };
        _db.Set<PeriodDefinition>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return new PeriodDefinitionDto(entity.PeriodNumber, entity.Name, entity.StartTime.ToString(@"hh\:mm"), entity.EndTime.ToString(@"hh\:mm"), entity.IsBreak, entity.SortOrder);
    }

    public async Task BulkSavePeriodsAsync(long tenantId, long userId, BulkPeriodsRequest req, CancellationToken ct = default)
    {
        var existing = await _db.Set<PeriodDefinition>().Where(p=>p.TenantId==tenantId && !p.IsDeleted).ToListAsync(ct);
        _db.Set<PeriodDefinition>().RemoveRange(existing);
        await _db.SaveChangesAsync(ct);

        var entities = req.Periods.Select((p,i)=> new PeriodDefinition
        {
            TenantId=tenantId,
            PeriodNumber=i+1,
            Name=p.Name,
            StartTime=TimeSpan.Parse(p.StartTime),
            EndTime=TimeSpan.Parse(p.EndTime),
            IsBreak=p.IsBreak,
            SortOrder=p.SortOrder,
            AcademicYearId=p.AcademicYearId,
            CreatedBy=userId
        }).ToList();

        _db.Set<PeriodDefinition>().AddRange(entities);
        await _db.SaveChangesAsync(ct);
    }

    // Timetable effective dated - mid-term change does not rewrite history
    public async Task<TimetableDto> CreateTimetableAsync(long tenantId, long userId, CreateTimetableRequest req, CancellationToken ct = default)
    {
        // If effective_from overlaps existing active timetable, archive previous? For V1, allow multiple versions, but effective dates must not overlap for same academic year/term
        var overlapping = await _db.Set<Timetable>()
            .Where(t=>t.TenantId==tenantId && t.AcademicYearId==req.AcademicYearId && t.TermId==req.TermId && t.Status=="active" && !t.IsDeleted &&
                      (t.EffectiveTo==null || t.EffectiveTo>=req.EffectiveFrom) &&
                      (req.EffectiveTo==null || t.EffectiveFrom<=req.EffectiveTo))
            .AnyAsync(ct);

        if (overlapping)
        {
            // For effective dated, we allow new version but must set previous EffectiveTo = new EffectiveFrom -1 day
            var activeTimetables = await _db.Set<Timetable>()
                .Where(t=>t.TenantId==tenantId && t.AcademicYearId==req.AcademicYearId && t.TermId==req.TermId && t.Status=="active" && !t.IsDeleted && t.EffectiveTo==null)
                .ToListAsync(ct);
            foreach(var at in activeTimetables)
            {
                at.EffectiveTo = req.EffectiveFrom.AddDays(-1);
            }
            await _db.SaveChangesAsync(ct);
        }

        var version = await _db.Set<Timetable>().Where(t=>t.TenantId==tenantId && t.AcademicYearId==req.AcademicYearId && t.TermId==req.TermId && !t.IsDeleted).CountAsync(ct) + 1;

        var timetable = new Timetable
        {
            TenantId=tenantId,
            Name=req.Name,
            AcademicYearId=req.AcademicYearId,
            TermId=req.TermId,
            EffectiveFrom=req.EffectiveFrom,
            EffectiveTo=req.EffectiveTo,
            Version=version,
            Status="active",
            CreatedFromTimetableId=req.CloneFromTimetableId,
            CreatedBy=userId
        };
        _db.Set<Timetable>().Add(timetable);
        await _db.SaveChangesAsync(ct);

        // If clone, copy slots
        if (req.CloneFromTimetableId.HasValue)
        {
            var sourceSlots = await _db.Set<TimetableSlot>().Where(s=>s.TimetableId==req.CloneFromTimetableId.Value && s.TenantId==tenantId && !s.IsDeleted).ToListAsync(ct);
            var cloned = sourceSlots.Select(s=> new TimetableSlot
            {
                TenantId=tenantId,
                TimetableId=timetable.Id,
                AcademicYearId=s.AcademicYearId,
                TermId=s.TermId,
                GradeId=s.GradeId,
                StreamId=s.StreamId,
                SubjectId=s.SubjectId,
                TeacherStaffId=s.TeacherStaffId,
                RoomId=s.RoomId,
                DayOfWeek=s.DayOfWeek,
                PeriodNumber=s.PeriodNumber,
                StartTime=s.StartTime,
                EndTime=s.EndTime,
                CreatedBy=userId
            }).ToList();
            _db.Set<TimetableSlot>().AddRange(cloned);
            await _db.SaveChangesAsync(ct);
        }

        return new TimetableDto(timetable.Id, timetable.Name, timetable.AcademicYearId, timetable.TermId, timetable.EffectiveFrom, timetable.EffectiveTo, timetable.Version, timetable.Status, 0);
    }

    public async Task<TimetableDto> GetTimetableAsync(long tenantId, long timetableId, CancellationToken ct = default)
    {
        var t = await _db.Set<Timetable>().FirstOrDefaultAsync(x=>x.Id==timetableId && x.TenantId==tenantId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Timetable not found");
        var count = await _db.Set<TimetableSlot>().CountAsync(s=>s.TimetableId==t.Id && s.TenantId==tenantId && !s.IsDeleted, ct);
        return new TimetableDto(t.Id, t.Name, t.AcademicYearId, t.TermId, t.EffectiveFrom, t.EffectiveTo, t.Version, t.Status, count);
    }

    public async Task<List<TimetableDto>> ListTimetablesAsync(long tenantId, long academicYearId, long termId, CancellationToken ct = default)
    {
        var list = await _db.Set<Timetable>().Where(t=>t.TenantId==tenantId && t.AcademicYearId==academicYearId && t.TermId==termId && !t.IsDeleted).OrderByDescending(t=>t.Version).ToListAsync(ct);
        var result = new List<TimetableDto>();
        foreach(var t in list)
        {
            var count = await _db.Set<TimetableSlot>().CountAsync(s=>s.TimetableId==t.Id && !s.IsDeleted, ct);
            result.Add(new TimetableDto(t.Id, t.Name, t.AcademicYearId, t.TermId, t.EffectiveFrom, t.EffectiveTo, t.Version, t.Status, count));
        }
        return result;
    }

    public async Task<ClashResponseDto> CheckClashAsync(long tenantId, long timetableId, CreateSlotRequest newSlot, long? excludeSlotId = null, CancellationToken ct = default)
    {
        var timetable = await _db.Set<Timetable>().FirstOrDefaultAsync(t=>t.Id==timetableId && t.TenantId==tenantId && !t.IsDeleted, ct) ?? throw new InvalidOperationException("Timetable not found");
        var existingSlots = await _db.Set<TimetableSlot>()
            .Where(s=>s.TimetableId==timetableId && s.TenantId==tenantId && !s.IsDeleted && s.Id!=excludeSlotId)
            .Include(s=>s.Timetable)
            .ToListAsync(ct);

        var clashes = new List<ClashDetailDto>();

        // Need names for plain language
        var grade = await _db.Grades.FirstOrDefaultAsync(g=>g.Id==newSlot.GradeId, ct);
        var stream = await _db.Streams.FirstOrDefaultAsync(s=>s.Id==newSlot.StreamId, ct);
        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s=>s.Id==newSlot.SubjectId, ct);
        var teacher = await _db.Set<StaffProfile>().FirstOrDefaultAsync(s=>s.Id==newSlot.TeacherStaffId, ct);
        var room = newSlot.RoomId.HasValue ? await _db.Set<Room>().FirstOrDefaultAsync(r=>r.Id==newSlot.RoomId.Value, ct) : null;

        string newInfo = $"{grade?.Name ?? newSlot.GradeId} {stream?.Name ?? newSlot.StreamId} {subject?.Name ?? newSlot.SubjectId} with {teacher?.FirstName ?? ""} {teacher?.LastName ?? newSlot.TeacherStaffId} in {(room?.Name ?? "no room")}";

        foreach(var existing in existingSlots)
        {
            if (existing.DayOfWeek != newSlot.DayOfWeek || existing.PeriodNumber != newSlot.PeriodNumber) continue;

            // Teacher clash
            if (existing.TeacherStaffId == newSlot.TeacherStaffId)
            {
                var exGrade = await _db.Grades.FirstOrDefaultAsync(g=>g.Id==existing.GradeId, ct);
                var exStream = await _db.Streams.FirstOrDefaultAsync(s=>s.Id==existing.StreamId, ct);
                var exSubject = await _db.Set<Subject>().FirstOrDefaultAsync(s=>s.Id==existing.SubjectId, ct);
                var exTeacher = await _db.Set<StaffProfile>().FirstOrDefaultAsync(s=>s.Id==existing.TeacherStaffId, ct);
                var exRoom = existing.RoomId.HasValue ? await _db.Set<Room>().FirstOrDefaultAsync(r=>r.Id==existing.RoomId.Value, ct) : null;
                string exInfo = $"{exGrade?.Name} {exStream?.Name} {exSubject?.Name} with {exTeacher?.FirstName} {exTeacher?.LastName} in {(exRoom?.Name ?? "no room")}";

                clashes.Add(new ClashDetailDto(
                    "Teacher",
                    $"Teacher {teacher?.FirstName} {teacher?.LastName} is already teaching {exInfo} at {DayName(existing.DayOfWeek)} Period {existing.PeriodNumber}. A teacher cannot be in two places at once.",
                    existing.Id,
                    exInfo,
                    0,
                    newInfo,
                    existing.DayOfWeek,
                    existing.PeriodNumber,
                    existing.TeacherStaffId,
                    $"{exTeacher?.FirstName} {exTeacher?.LastName}",
                    existing.GradeId,
                    exGrade?.Name ?? "",
                    existing.StreamId,
                    exStream?.Name ?? "",
                    existing.RoomId,
                    exRoom?.Name
                ));
            }

            // Class double-booked
            if (existing.GradeId == newSlot.GradeId && existing.StreamId == newSlot.StreamId)
            {
                var exSubject = await _db.Set<Subject>().FirstOrDefaultAsync(s=>s.Id==existing.SubjectId, ct);
                var exTeacher = await _db.Set<StaffProfile>().FirstOrDefaultAsync(s=>s.Id==existing.TeacherStaffId, ct);
                string exInfo = $"{grade?.Name} {stream?.Name} already has {exSubject?.Name} with {exTeacher?.FirstName} at {DayName(existing.DayOfWeek)} Period {existing.PeriodNumber}. Class double-booked.";

                clashes.Add(new ClashDetailDto(
                    "Class",
                    $"Class {grade?.Name} {stream?.Name} is already booked for {exSubject?.Name} with {exTeacher?.FirstName} {exTeacher?.LastName} at {DayName(existing.DayOfWeek)} Period {existing.PeriodNumber}. A class cannot have two lessons at the same time.",
                    existing.Id,
                    exInfo,
                    0,
                    newInfo,
                    existing.DayOfWeek,
                    existing.PeriodNumber,
                    existing.TeacherStaffId,
                    $"{exTeacher?.FirstName} {exTeacher?.LastName}",
                    existing.GradeId,
                    grade?.Name ?? "",
                    existing.StreamId,
                    stream?.Name ?? "",
                    existing.RoomId,
                    null
                ));
            }

            // Room conflict if rooms used
            if (newSlot.RoomId.HasValue && existing.RoomId.HasValue && existing.RoomId.Value == newSlot.RoomId.Value)
            {
                var exGrade = await _db.Grades.FirstOrDefaultAsync(g=>g.Id==existing.GradeId, ct);
                var exStream = await _db.Streams.FirstOrDefaultAsync(s=>s.Id==existing.StreamId, ct);
                clashes.Add(new ClashDetailDto(
                    "Room",
                    $"Room {room?.Name} is already booked by {exGrade?.Name} {exStream?.Name} at {DayName(existing.DayOfWeek)} Period {existing.PeriodNumber}. Room conflict — two classes cannot use the same room at the same time.",
                    existing.Id,
                    $"{exGrade?.Name} {exStream?.Name} in {room?.Name}",
                    0,
                    newInfo,
                    existing.DayOfWeek,
                    existing.PeriodNumber,
                    existing.TeacherStaffId,
                    "",
                    existing.GradeId,
                    exGrade?.Name ?? "",
                    existing.StreamId,
                    exStream?.Name ?? "",
                    existing.RoomId,
                    room?.Name
                ));
            }
        }

        return new ClashResponseDto(clashes.Any(), clashes);
    }

    public async Task<TimetableSlotDto> CreateSlotAsync(long tenantId, long userId, long timetableId, CreateSlotRequest req, CancellationToken ct = default)
    {
        var clash = await CheckClashAsync(tenantId, timetableId, req, null, ct);
        if (clash.HasClash)
        {
            // Do not silently refuse - return clash info
            throw new InvalidOperationException($"Clash detected: {string.Join(" | ", clash.Clashes.Select(c=>c.Message))}");
        }

        var period = await _db.Set<PeriodDefinition>().FirstOrDefaultAsync(p=>p.TenantId==tenantId && p.PeriodNumber==req.PeriodNumber && !p.IsDeleted, ct);
        var timetable = await _db.Set<Timetable>().FirstAsync(t=>t.Id==timetableId, ct);

        var slot = new TimetableSlot
        {
            TenantId=tenantId,
            TimetableId=timetableId,
            AcademicYearId=timetable.AcademicYearId,
            TermId=timetable.TermId,
            GradeId=req.GradeId,
            StreamId=req.StreamId,
            SubjectId=req.SubjectId,
            TeacherStaffId=req.TeacherStaffId,
            RoomId=req.RoomId,
            DayOfWeek=req.DayOfWeek,
            PeriodNumber=req.PeriodNumber,
            StartTime=period?.StartTime ?? new TimeSpan(8,0,0),
            EndTime=period?.EndTime ?? new TimeSpan(8,45,0),
            CreatedBy=userId
        };
        _db.Set<TimetableSlot>().Add(slot);
        await _db.SaveChangesAsync(ct);

        return await MapSlotToDto(slot, ct);
    }

    public async Task<ClashResponseDto> BulkCreateSlotsAsync(long tenantId, long userId, long timetableId, BulkSlotsRequest req, CancellationToken ct = default)
    {
        var allClashes = new List<ClashDetailDto>();
        var created = 0;

        foreach(var slotReq in req.Slots)
        {
            var clash = await CheckClashAsync(tenantId, timetableId, slotReq, null, ct);
            if (clash.HasClash)
            {
                allClashes.AddRange(clash.Clashes);
            }
            else
            {
                var period = await _db.Set<PeriodDefinition>().FirstOrDefaultAsync(p=>p.TenantId==tenantId && p.PeriodNumber==slotReq.PeriodNumber, ct);
                var timetable = await _db.Set<Timetable>().FirstAsync(t=>t.Id==timetableId, ct);
                var slot = new TimetableSlot
                {
                    TenantId=tenantId,
                    TimetableId=timetableId,
                    AcademicYearId=timetable.AcademicYearId,
                    TermId=timetable.TermId,
                    GradeId=slotReq.GradeId,
                    StreamId=slotReq.StreamId,
                    SubjectId=slotReq.SubjectId,
                    TeacherStaffId=slotReq.TeacherStaffId,
                    RoomId=slotReq.RoomId,
                    DayOfWeek=slotReq.DayOfWeek,
                    PeriodNumber=slotReq.PeriodNumber,
                    StartTime=period?.StartTime ?? new TimeSpan(8,0,0),
                    EndTime=period?.EndTime ?? new TimeSpan(8,45,0),
                    CreatedBy=userId
                };
                _db.Set<TimetableSlot>().Add(slot);
                created++;
            }
        }

        if (created>0) await _db.SaveChangesAsync(ct);

        return new ClashResponseDto(allClashes.Any(), allClashes);
    }

    public async Task DeleteSlotAsync(long tenantId, long timetableId, long slotId, CancellationToken ct = default)
    {
        var slot = await _db.Set<TimetableSlot>().FirstOrDefaultAsync(s=>s.Id==slotId && s.TimetableId==timetableId && s.TenantId==tenantId && !s.IsDeleted, ct) ?? throw new InvalidOperationException("Slot not found");
        _db.Set<TimetableSlot>().Remove(slot); // soft delete via SaveChanges
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TimetableGridDto> GetGridAsync(long tenantId, TimetableViewRequest req, CancellationToken ct = default)
    {
        // Find effective timetable for date
        var effectiveDate = req.EffectiveDate ?? DateTime.UtcNow.Date;
        var timetable = await FindEffectiveTimetable(tenantId, req.AcademicYearId, req.TermId, effectiveDate, req.TimetableId, ct);

        if (timetable == null) throw new InvalidOperationException("No effective timetable found for date");

        var periods = await GetPeriodsAsync(tenantId, req.AcademicYearId, ct);

        var slotsQuery = _db.Set<TimetableSlot>().Where(s=>s.TimetableId==timetable.Id && s.TenantId==tenantId && !s.IsDeleted);

        if (req.GradeId.HasValue) slotsQuery = slotsQuery.Where(s=>s.GradeId==req.GradeId.Value);
        if (req.StreamId.HasValue) slotsQuery = slotsQuery.Where(s=>s.StreamId==req.StreamId.Value);
        if (req.TeacherStaffId.HasValue) slotsQuery = slotsQuery.Where(s=>s.TeacherStaffId==req.TeacherStaffId.Value);

        var slots = await slotsQuery.ToListAsync(ct);
        var slotDtos = new List<TimetableSlotDto>();
        foreach(var s in slots)
        {
            slotDtos.Add(await MapSlotToDto(s, ct));
        }

        var days = Enumerable.Range(1,5).Select(d=> new DayColumnDto(d, DayName(d), slotDtos.Where(s=>s.DayOfWeek==d).OrderBy(s=>s.PeriodNumber).ToList())).ToList();

        return new TimetableGridDto(timetable.Id, timetable.Name, periods, days);
    }

    public Task<TimetableGridDto> GetByClassAsync(long tenantId, long gradeId, long streamId, long academicYearId, long termId, DateTime? effectiveDate, CancellationToken ct = default)
        => GetGridAsync(tenantId, new TimetableViewRequest(gradeId, streamId, null, academicYearId, termId, effectiveDate, null), ct);

    public Task<TimetableGridDto> GetByTeacherAsync(long tenantId, long teacherStaffId, long academicYearId, long termId, DateTime? effectiveDate, CancellationToken ct = default)
        => GetGridAsync(tenantId, new TimetableViewRequest(null, null, teacherStaffId, academicYearId, termId, effectiveDate, null), ct);

    private async Task<Timetable?> FindEffectiveTimetable(long tenantId, long academicYearId, long termId, DateTime date, long? timetableId, CancellationToken ct)
    {
        if (timetableId.HasValue)
            return await _db.Set<Timetable>().FirstOrDefaultAsync(t=>t.Id==timetableId.Value && t.TenantId==tenantId && !t.IsDeleted, ct);

        // Find timetable where effective_from <= date and (effective_to null or >= date) and status active, order by version desc
        return await _db.Set<Timetable>()
            .Where(t=>t.TenantId==tenantId && t.AcademicYearId==academicYearId && t.TermId==termId && t.Status=="active" && !t.IsDeleted && t.EffectiveFrom<=date && (t.EffectiveTo==null || t.EffectiveTo>=date))
            .OrderByDescending(t=>t.Version)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<TimetableSlotDto> MapSlotToDto(TimetableSlot slot, CancellationToken ct)
    {
        var grade = await _db.Grades.FirstOrDefaultAsync(g=>g.Id==slot.GradeId, ct);
        var stream = await _db.Streams.FirstOrDefaultAsync(s=>s.Id==slot.StreamId, ct);
        var subject = await _db.Set<Subject>().FirstOrDefaultAsync(s=>s.Id==slot.SubjectId, ct);
        var teacher = await _db.Set<StaffProfile>().FirstOrDefaultAsync(s=>s.Id==slot.TeacherStaffId, ct);
        var room = slot.RoomId.HasValue ? await _db.Set<Room>().FirstOrDefaultAsync(r=>r.Id==slot.RoomId.Value, ct) : null;
        var period = await _db.Set<PeriodDefinition>().FirstOrDefaultAsync(p=>p.TenantId==slot.TenantId && p.PeriodNumber==slot.PeriodNumber, ct);

        return new TimetableSlotDto(
            slot.Id,
            slot.TimetableId,
            slot.GradeId,
            grade?.Name ?? $"Grade {slot.GradeId}",
            slot.StreamId,
            stream?.Name ?? $"Stream {slot.StreamId}",
            slot.SubjectId,
            subject?.Name ?? $"Subject {slot.SubjectId}",
            slot.TeacherStaffId,
            teacher != null ? $"{teacher.FirstName} {teacher.LastName}" : $"Teacher {slot.TeacherStaffId}",
            slot.RoomId,
            room?.Name,
            slot.DayOfWeek,
            DayName(slot.DayOfWeek),
            slot.PeriodNumber,
            period?.Name ?? $"Period {slot.PeriodNumber}",
            slot.StartTime.ToString(@"hh\:mm"),
            slot.EndTime.ToString(@"hh\:mm")
        );
    }

    private static string DayName(int day) => day switch {1=>"Monday",2=>"Tuesday",3=>"Wednesday",4=>"Thursday",5=>"Friday",6=>"Saturday",7=>"Sunday",_=> $"Day {day}"};
}
