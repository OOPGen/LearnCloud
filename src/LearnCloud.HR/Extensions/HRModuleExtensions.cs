using LearnCloud.HR.Services;

namespace LearnCloud.HR.Extensions;

public static class HRModuleExtensions
{
    public static IServiceCollection AddLearnCloudHR(this IServiceCollection services)
    {
        services.AddScoped<IHRService, HRService>();
        return services;
    }
}
