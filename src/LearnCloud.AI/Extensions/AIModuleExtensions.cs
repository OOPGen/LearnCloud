using LearnCloud.AI.Services;
using LearnCloud.AI.Services.Providers;

namespace LearnCloud.AI.Extensions;

public static class AIModuleExtensions
{
    public static IServiceCollection AddLearnCloudAI(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IReportCommentService, ReportCommentService>();
        services.AddScoped<IAttendanceAnomalyService, AttendanceAnomalyService>();
        services.AddScoped<IAtRiskService, AtRiskService>();
        services.AddScoped<IAIProviderFactory, AIProviderFactory>();
        services.AddScoped<RuleBasedAIProvider>();

        // OpenAIProvider takes its key and model as constructor strings, which the
        // container cannot supply, so resolving it by type returned null. With no key
        // configured the provider itself falls back to rule-based output.
        services.AddHttpClient(nameof(OpenAIProvider));
        services.AddScoped(sp => new OpenAIProvider(
            config["AI:OpenAI:ApiKey"] ?? string.Empty,
            config["AI:OpenAI:Model"] ?? "gpt-4o-mini",
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenAIProvider)),
            sp.GetRequiredService<ILogger<OpenAIProvider>>()));
        return services;
    }
}
