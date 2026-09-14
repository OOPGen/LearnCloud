using LearnCloud.SetupWizard.Services;

namespace LearnCloud.SetupWizard.Extensions;

public static class SetupWizardModuleExtensions
{
    public static IServiceCollection AddLearnCloudSetupWizard(this IServiceCollection services)
    {
        services.AddScoped<IWizardService, WizardService>();
        return services;
    }
}
