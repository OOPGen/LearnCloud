using LearnCloud.Infrastructure.Email;
using LearnCloud.Infrastructure.Jobs;

namespace LearnCloud.Infrastructure;

public static class InfrastructureExtensions
{
    /// <summary>
    /// Registers the job queue, the job and schedule workers (active unless Jobs:Enabled is
    /// false) and email delivery for the provider in Email:Provider.
    /// </summary>
    public static IServiceCollection AddLearnCloudInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JobOptions>(config.GetSection("Jobs"));
        services.Configure<AppOptions>(config.GetSection("App"));
        services.AddOptions<EmailOptions>().Bind(config.GetSection("Email"))
            .Validate(o => !o.Problems().Any(), "Email settings are invalid")
            .ValidateOnStart();

        services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
        services.AddSingleton<BackgroundJobRunner>();
        services.AddSingleton<ScheduledJobRunner>();
        services.AddHostedService<BackgroundJobWorker>();
        services.AddHostedService<ScheduledJobWorker>();
        services.AddSingleton<IScheduledJob, JobCleanupScheduledJob>();

        services.AddScoped<IEmailOutbox, EmailOutbox>();
        services.AddScoped<IBackgroundJobHandler, EmailJobHandler>();
        services.AddHostedService<EmailConfigurationCheck>();

        var email = config.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        var problems = email.Problems().ToList();
        if (problems.Count > 0)
            throw new InvalidOperationException("Email configuration is invalid: " + string.Join(" ", problems));

        switch (email.Provider.Trim().ToLowerInvariant())
        {
            case "resend":
                services.AddHttpClient<IEmailDelivery, ResendEmailDelivery>(http =>
                {
                    http.BaseAddress = new Uri(email.Resend.BaseUrl);
                    http.Timeout = TimeSpan.FromSeconds(30);
                });
                break;
            case "smtp":
                services.AddScoped<IEmailDelivery, SmtpEmailDelivery>();
                break;
            default:
                services.AddScoped<IEmailDelivery, LogEmailDelivery>();
                break;
        }
        return services;
    }
}
