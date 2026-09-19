using LearnCloud.Infrastructure.Jobs;
using LearnCloud.Messaging.Jobs;
using LearnCloud.Messaging.Services;
using LearnCloud.Messaging.Services.Providers;

namespace LearnCloud.Messaging.Extensions;

public static class MessagingModuleExtensions
{
    public static IServiceCollection AddLearnCloudMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<MessagingCostOptions>(config.GetSection("Messaging:Costs"));

        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<AudienceResolver>();
        services.AddScoped<IMessagingProviderFactory, MessagingProviderFactory>();
        services.AddScoped<PlatformEmailProvider>();
        services.AddScoped<UnconfiguredSmsProvider>();

        services.AddScoped<MessagingBackgroundJob>();
        services.AddScoped<IMessageBatchQueue, MessageBatchQueue>();
        services.AddScoped<IBackgroundJobHandler, SendMessageBatchJobHandler>();
        return services;
    }
}
