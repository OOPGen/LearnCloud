using LearnCloud.ParentPortal.Services;
using LearnCloud.ParentPortal.Services.ParentTeacherMessaging;

namespace LearnCloud.ParentPortal.Extensions;

public static class ParentPortalModuleExtensions
{
    public static IServiceCollection AddLearnCloudParentPortal(this IServiceCollection services)
    {
        services.AddScoped<IParentAuthorizationService, ParentAuthorizationService>();
        services.AddScoped<IParentPortalService, ParentPortalService>();
        services.AddScoped<ParentTeacherMessagingService>();
        return services;
    }
}
