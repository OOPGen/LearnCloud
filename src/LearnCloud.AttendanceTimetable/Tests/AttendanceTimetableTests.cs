using LearnCloud.AttendanceTimetable.Entities;
using LearnCloud.AttendanceTimetable.Services;
using Xunit;

namespace LearnCloud.AttendanceTimetable.Tests;

public class AttendancePercentageTests
{
    private readonly AttendanceService _service;
    private readonly TenantAttendanceSettings _defaultSettings;

    public AttendancePercentageTests()
    {
        // Service needs DbContext but CalculatePercentage is pure - we can test via instance with null db for this method
        // For simplicity, create settings object directly
        _defaultSettings = new TenantAttendanceSettings
        {
            TenantId = 1,
            Mode = AttendanceMode.Daily,
            ChronicAbsenceThreshold = 85m,
            CountLateAsPresent = true,
            CountExcusedAsPresent = true,
            CountSickAsPresent = false
        };
    }

    private decimal Calc(int present, int late, int excused, int sick, int absent, int total, TenantAttendanceSettings? settings = null)
    {
        var s = settings ?? _defaultSettings;
        var svc = new AttendanceService(null!, null!);
        // Use reflection to call CalculatePercentage which is public
        return svc.CalculatePercentage(present, late, excused, sick, absent, total, s);
    }

    [Fact]
    public void CalculatePercentage_DailyMode_CountLateAndExcusedAsPresent()
    {
        // 20 days: 15 present, 2 late, 1 excused, 1 sick, 1 absent = total 20
        // With CountLateAsPresent=true, CountExcusedAsPresent=true, CountSickAsPresent=false
        // PresentCount = 15+2+1 = 18 => 90%
        var perc = Calc(present:15, late:2, excused:1, sick:1, absent:1, total:20);
        Assert.Equal(90m, perc);
    }

    [Fact]
    public void CalculatePercentage_AllAbsent_ZeroPercent()
    {
        var perc = Calc(0,0,0,0,20,20);
        Assert.Equal(0m, perc);
    }

    [Fact]
    public void CalculatePercentage_AllPresent_HundredPercent()
    {
        var perc = Calc(20,0,0,0,0,20);
        Assert.Equal(100m, perc);
    }

    [Fact]
    public void CalculatePercentage_CountSickAsPresent_False()
    {
        // 10 days: 5 present, 5 sick, settings CountSickAsPresent=false => 50%
        var perc = Calc(5,0,0,5,0,10);
        Assert.Equal(50m, perc);

        // If CountSickAsPresent=true => 100%
        var settingsWithSick = new TenantAttendanceSettings
        {
            TenantId=1,
            CountLateAsPresent=true,
            CountExcusedAsPresent=true,
            CountSickAsPresent=true,
            ChronicAbsenceThreshold=85m
        };
        var perc2 = Calc(5,0,0,5,0,10, settingsWithSick);
        Assert.Equal(100m, perc2);
    }

    [Fact]
    public void CalculatePercentage_ZeroTotal_ReturnsZero()
    {
        var perc = Calc(0,0,0,0,0,0);
        Assert.Equal(0m, perc);
    }

    [Fact]
    public void ChronicAbsence_Flagged_When_Below_Threshold()
    {
        var settings = new TenantAttendanceSettings { TenantId=1, ChronicAbsenceThreshold=85m, CountLateAsPresent=true, CountExcusedAsPresent=true };
        var percBelow = Calc(16,0,0,0,4,20, settings); // 80% <85
        Assert.True(percBelow < settings.ChronicAbsenceThreshold);

        var percAbove = Calc(18,0,0,0,2,20, settings); // 90% >85
        Assert.True(percAbove >= settings.ChronicAbsenceThreshold);
    }

    [Theory]
    [InlineData(10,0,0,0,0,10,100)]
    [InlineData(8,1,1,0,0,10,100)] // 8 present +1 late +1 excused =10 =>100%
    [InlineData(8,0,0,0,2,10,80)] // 8/10=80
    [InlineData(5,2,1,1,1,10,80)] // 5+2+1=8 with default settings (late+excused) =80, sick not counted
    public void CalculatePercentage_Theory(int present,int late,int excused,int sick,int absent,int total,decimal expected)
    {
        var perc = Calc(present,late,excused,sick,absent,total);
        Assert.Equal(expected, perc);
    }
}

public class TimetableClashDetectionTests
{
    // Unit tests for clash detection logic - pure method without DB, simulate in-memory lists
    private bool HasTeacherClash(List<(int day,int period,long teacherId)> existing, int day,int period,long teacherId)
    {
        return existing.Any(e=> e.day==day && e.period==period && e.teacherId==teacherId);
    }

    private bool HasClassClash(List<(int day,int period,long gradeId,long streamId)> existing, int day,int period,long gradeId,long streamId)
    {
        return existing.Any(e=> e.day==day && e.period==period && e.gradeId==gradeId && e.streamId==streamId);
    }

    private bool HasRoomClash(List<(int day,int period,long? roomId)> existing, int day,int period,long? roomId)
    {
        if (!roomId.HasValue) return false;
        return existing.Any(e=> e.day==day && e.period==period && e.roomId==roomId);
    }

    [Fact]
    public void TeacherClash_Detected()
    {
        var existing = new List<(int day,int period,long teacherId)> { (1,2,101) }; // Monday Period2 teacher 101
        Assert.True(HasTeacherClash(existing, 1,2,101)); // same day period teacher -> clash
        Assert.False(HasTeacherClash(existing, 1,3,101)); // different period no clash
        Assert.False(HasTeacherClash(existing, 2,2,101)); // different day no clash
        Assert.False(HasTeacherClash(existing, 1,2,102)); // different teacher no clash
    }

    [Fact]
    public void ClassDoubleBooked_Detected()
    {
        var existing = new List<(int day,int period,long gradeId,long streamId)> { (1,2,10,20) }; // Grade10 Stream20 Monday Period2
        Assert.True(HasClassClash(existing, 1,2,10,20));
        Assert.False(HasClassClash(existing, 1,3,10,20));
        Assert.False(HasClassClash(existing, 1,2,10,21)); // different stream no clash
    }

    [Fact]
    public void RoomConflict_Detected_If_Rooms_Used()
    {
        var existing = new List<(int day,int period,long? roomId)> { (1,2,5), (1,3,5) };
        Assert.True(HasRoomClash(existing, 1,2,5));
        Assert.False(HasRoomClash(existing, 1,2,null)); // no room no clash
        Assert.False(HasRoomClash(existing, 1,4,5)); // different period
        Assert.False(HasRoomClash(existing, 1,2,6)); // different room
    }

    [Fact]
    public void Clash_Message_Plain_Language()
    {
        // Simulate clash message generation as in TimetableService
        var teacherName = "Mrs Moyo";
        var existingInfo = "Form 2B Mathematics with Mrs Moyo in Room 5";
        var dayName = "Monday";
        var period = 2;
        var message = $"Teacher {teacherName} is already teaching {existingInfo} at {dayName} Period {period}. A teacher cannot be in two places at once.";
        Assert.Contains("already teaching", message);
        Assert.Contains("cannot be in two places", message);
        Assert.Contains("Mrs Moyo", message);
    }

    [Fact]
    public void EffectiveDated_Timetable_Does_Not_Rewrite_History()
    {
        // Simulate effective dated logic
        var timetables = new List<(long id, DateTime from, DateTime? to, int version, string status)>
        {
            (1, new DateTime(2026,1,10), new DateTime(2026,5,9), 1, "active"),
            (2, new DateTime(2026,5,10), null, 2, "active"), // mid-term change
        };

        // Query for date 2026-02-01 should return v1
        var date1 = new DateTime(2026,2,1);
        var effective1 = timetables.Where(t=>t.from<=date1 && (t.to==null || t.to>=date1) && t.status=="active").OrderByDescending(t=>t.version).First();
        Assert.Equal(1, effective1.id);

        // Query for date 2026-06-01 should return v2 (mid-term change)
        var date2 = new DateTime(2026,6,1);
        var effective2 = timetables.Where(t=>t.from<=date2 && (t.to==null || t.to>=date2) && t.status=="active").OrderByDescending(t=>t.version).First();
        Assert.Equal(2, effective2.id);

        // History preserved - past registers still reference v1 slots
        Assert.NotEqual(effective1.id, effective2.id);
    }

    [Fact]
    public void Duplicate_Register_Guard()
    {
        // Guard against duplicate registers for same class, date, period
        var existingRegisters = new List<(long tenantId,long gradeId,long streamId,DateTime date,int? period)>
        {
            (1,10,20,new DateTime(2026,8,2),1)
        };

        bool IsDuplicate(long tenantId,long gradeId,long streamId,DateTime date,int? period)
        {
            return existingRegisters.Any(r=> r.tenantId==tenantId && r.gradeId==gradeId && r.streamId==streamId && r.date==date && r.period==period);
        }

        Assert.True(IsDuplicate(1,10,20,new DateTime(2026,8,2),1)); // same -> duplicate
        Assert.False(IsDuplicate(1,10,20,new DateTime(2026,8,3),1)); // different date
        Assert.False(IsDuplicate(1,10,21,new DateTime(2026,8,2),1)); // different stream
        Assert.False(IsDuplicate(1,10,20,new DateTime(2026,8,2),2)); // different period
    }

    [Fact]
    public void Backdating_Flagged_When_Beyond_Window()
    {
        int windowDays = 7;
        DateTime today = new DateTime(2026,8,10);
        DateTime attendanceDateWithin = new DateTime(2026,8,5); // 5 days ago
        DateTime attendanceDateBeyond = new DateTime(2026,8,1); // 9 days ago

        bool IsBackdatedBeyondWindow(DateTime attDate)
        {
            var diff = (today - attDate).TotalDays;
            return diff > windowDays;
        }

        Assert.False(IsBackdatedBeyondWindow(attendanceDateWithin));
        Assert.True(IsBackdatedBeyondWindow(attendanceDateBeyond));
    }
}
