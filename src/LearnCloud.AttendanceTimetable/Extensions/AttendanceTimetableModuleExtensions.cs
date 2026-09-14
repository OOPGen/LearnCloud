using LearnCloud.AttendanceTimetable.Services;

namespace LearnCloud.AttendanceTimetable.Extensions;

public static class AttendanceTimetableModuleExtensions
{
    public static IServiceCollection AddLearnCloudAttendanceTimetable(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<ITimetableService, TimetableService>();
        return services;
    }
}
