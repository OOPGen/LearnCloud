using LearnCloud.Messaging.Jobs;
using LearnCloud.Messaging.Services;
using LearnCloud.Messaging.Services.Providers;

namespace LearnCloud.Messaging.Extensions;

public static class MessagingModuleExtensions
{
    public static IServiceCollection AddLearnCloudMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SmsProviderOptions>(config.GetSection("Messaging:Sms"));
        services.Configure<EmailProviderOptions>(config.GetSection("Messaging:Email"));

        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<AudienceResolver>();
        services.AddScoped<IMessagingProviderFactory, MessagingProviderFactory>();

        // The factory resolves concrete providers by type.
        services.AddHttpClient<EcoCashSmsProvider>();
        services.AddScoped<BulkSmsZwProvider>();
        services.AddScoped<SmtpEmailProvider>();
        services.AddScoped<SendGridEmailProvider>();

        services.AddScoped<MessagingBackgroundJob>();
        services.AddSingleton<IMessageBatchQueue, MessageBatchQueue>();
        services.AddHostedService<MessagingQueueWorker>();
        return services;
    }
}
