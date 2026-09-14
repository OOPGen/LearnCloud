using LearnCloud.StudentPortal.Services;

namespace LearnCloud.StudentPortal.Extensions;

public static class StudentPortalModuleExtensions
{
    public static IServiceCollection AddLearnCloudStudentPortal(this IServiceCollection services)
    {
        services.AddScoped<IStudentAuthorizationService, StudentAuthorizationService>();
        services.AddScoped<IStudentPortalService, StudentPortalService>();
        return services;
    }
}
