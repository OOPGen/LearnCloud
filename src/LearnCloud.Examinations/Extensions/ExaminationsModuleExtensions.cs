using LearnCloud.Examinations.Services;

namespace LearnCloud.Examinations.Extensions;

public static class ExaminationsModuleExtensions
{
    public static IServiceCollection AddLearnCloudExaminations(this IServiceCollection services)
    {
        services.AddScoped<ExaminationCalculationService>();
        return services;
    }
}
