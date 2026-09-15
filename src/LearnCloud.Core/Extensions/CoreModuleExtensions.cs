using LearnCloud.Core.Services;

namespace LearnCloud.Core.Extensions;

public static class CoreModuleExtensions
{
    public static IServiceCollection AddLearnCloudCore(this IServiceCollection services)
    {
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IAcademicCalendarService, AcademicCalendarService>();
        services.AddScoped<IClassStructureService, ClassStructureService>();
        services.AddScoped<IStudentRecordsService, StudentRecordsService>();
        services.AddScoped<IGuardianService, GuardianService>();
        return services;
    }
}
