using LearnCloud.Communication.Services;

namespace LearnCloud.Communication.Extensions;

public static class CommunicationModuleExtensions
{
    public static IServiceCollection AddLearnCloudCommunication(this IServiceCollection services)
    {
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IAudienceSegmentService, AudienceSegmentService>();
        services.AddScoped<ITemplateCategoryService, TemplateCategoryService>();
        services.AddScoped<ITwoWaySmsService, TwoWaySmsService>();
        services.AddScoped<ICommunicationRuleEngine, CommunicationRuleEngine>();
        return services;
    }
}
