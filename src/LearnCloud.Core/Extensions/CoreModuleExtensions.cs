using LearnCloud.Core.Services;

namespace LearnCloud.Core.Extensions;

public static class CoreModuleExtensions
{
    public static IServiceCollection AddLearnCloudCore(this IServiceCollection services)
    {
        services.AddScoped<ISubjectService, SubjectService>();
        return services;
    }
}
