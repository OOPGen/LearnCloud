using LearnCloud.TeacherPortal.Services;

namespace LearnCloud.TeacherPortal.Extensions;

public static class TeacherPortalModuleExtensions
{
    public static IServiceCollection AddLearnCloudTeacherPortal(this IServiceCollection services)
    {
        services.AddScoped<ITeacherAuthorizationService, TeacherAuthorizationService>();
        services.AddScoped<ITeacherDashboardService, TeacherDashboardService>();
        services.AddScoped<IMarksEntryService, MarksEntryService>();
        services.AddScoped<IHomeworkService, HomeworkService>();
        services.AddScoped<ILessonPlanService, LessonPlanService>();
        return services;
    }
}
