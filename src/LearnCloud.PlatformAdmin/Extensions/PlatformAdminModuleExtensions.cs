using LearnCloud.PlatformAdmin.Services;

namespace LearnCloud.PlatformAdmin.Extensions;

public static class PlatformAdminModuleExtensions
{
    public static IServiceCollection AddLearnCloudPlatformAdmin(this IServiceCollection services)
    {
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();
        services.AddScoped<IImpersonationService, ImpersonationService>();
        return services;
    }
}
