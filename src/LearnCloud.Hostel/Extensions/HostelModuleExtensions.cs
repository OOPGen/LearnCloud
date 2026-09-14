using LearnCloud.Hostel.Services;

namespace LearnCloud.Hostel.Extensions;

public static class HostelModuleExtensions
{
    public static IServiceCollection AddLearnCloudHostel(this IServiceCollection services)
    {
        services.AddScoped<IHostelService, HostelService>();
        return services;
    }
}
