using LearnCloud.Finance.Services;

namespace LearnCloud.Finance.Extensions;

public static class FinanceModuleExtensions
{
    public static IServiceCollection AddLearnCloudFinance(this IServiceCollection services)
    {
        services.AddScoped<FinanceCalculationService>();
        return services;
    }
}
