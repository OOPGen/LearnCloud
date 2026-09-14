using LearnCloud.PlatformBilling.Jobs;
using LearnCloud.PlatformBilling.Services;

namespace LearnCloud.PlatformBilling.Extensions;

public static class PlatformBillingModuleExtensions
{
    public static IServiceCollection AddLearnCloudPlatformBilling(this IServiceCollection services)
    {
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<DunningJob>();
        return services;
    }
}
